// apisurface <assembly>                  print the public surface, one sorted line per type and member
// apisurface <assembly> check <baseline> compare with the baseline file; print the difference and exit 1 if any
// apisurface <assembly> write <baseline> (re)write the baseline (after a change to the surface that is meant)
//
// "Public surface" = what another assembly can see without InternalsVisibleTo: public types (nested ones only inside
// public types) and their public or protected members. Compiler-generated members are left out.
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length != 1 && args.Length != 3) { Console.Error.WriteLine("usage: apisurface <assembly> [check|write <baseline>]"); return 2; }
using var pe = new PEReader(File.OpenRead(args[0]));
var md = pe.GetMetadataReader();
var prov = new Prov();
var lines = new List<string>();

string Full(TypeDefinition t)
{
    string ns = md.GetString(t.Namespace), n = md.GetString(t.Name);
    if (!t.GetDeclaringType().IsNil) return Full(md.GetTypeDefinition(t.GetDeclaringType())) + "+" + n;
    return ns.Length > 0 ? ns + "." + n : n;
}
bool Visible(TypeDefinition t)
{
    var v = t.Attributes & TypeAttributes.VisibilityMask;
    if (t.GetDeclaringType().IsNil) return v == TypeAttributes.Public;
    bool open = v == TypeAttributes.NestedPublic || v == TypeAttributes.NestedFamily || v == TypeAttributes.NestedFamORAssem;
    return open && Visible(md.GetTypeDefinition(t.GetDeclaringType()));
}
bool Open(MethodAttributes a) { var v = a & MethodAttributes.MemberAccessMask; return v == MethodAttributes.Public || v == MethodAttributes.Family || v == MethodAttributes.FamORAssem; }
bool OpenF(FieldAttributes a) { var v = a & FieldAttributes.FieldAccessMask; return v == FieldAttributes.Public || v == FieldAttributes.Family || v == FieldAttributes.FamORAssem; }
string Acc(MethodAttributes a) => (a & MethodAttributes.MemberAccessMask) == MethodAttributes.Public ? "public" : "protected";
bool Generated(string n) => n.Contains('<') || n.StartsWith("__");

foreach (var th in md.TypeDefinitions)
{
    var t = md.GetTypeDefinition(th);
    if (!Visible(t)) continue;
    string full = Full(t);
    if (Generated(full)) continue;
    string kind = (t.Attributes & TypeAttributes.Interface) != 0 ? "interface"
        : t.BaseType.IsNil ? "class" : BaseOf(t) switch { "System.Enum" => "enum", "System.ValueType" => "struct", "System.MulticastDelegate" => "delegate", _ => "class" };
    string mods = ((t.Attributes & TypeAttributes.Abstract) != 0 && (t.Attributes & TypeAttributes.Sealed) != 0) ? "static "
        : (t.Attributes & TypeAttributes.Sealed) != 0 && kind == "class" ? "sealed "
        : (t.Attributes & TypeAttributes.Abstract) != 0 && kind == "class" ? "abstract " : "";
    string baseName = kind == "class" && !t.BaseType.IsNil && BaseOf(t) != "System.Object" ? " : " + BaseOf(t) : "";
    lines.Add($"{full} | {mods}{kind}{baseName}");
    var accessors = new HashSet<MethodDefinitionHandle>();
    foreach (var ph in t.GetProperties())
    {
        var p = md.GetPropertyDefinition(ph);
        var acc = p.GetAccessors();
        accessors.Add(acc.Getter); accessors.Add(acc.Setter);
        string get = !acc.Getter.IsNil && Open(md.GetMethodDefinition(acc.Getter).Attributes) ? "get; " : "";
        string set = !acc.Setter.IsNil && Open(md.GetMethodDefinition(acc.Setter).Attributes) ? "set; " : "";
        if (get == "" && set == "") continue;
        var any = md.GetMethodDefinition(!acc.Getter.IsNil ? acc.Getter : acc.Setter);
        string st = (any.Attributes & MethodAttributes.Static) != 0 ? "static " : "";
        lines.Add($"{full} | {st}prop {p.DecodeSignature(prov, null).ReturnType} {md.GetString(p.Name)} {{ {get}{set}}}");
    }
    foreach (var eh in t.GetEvents())
    {
        var e = md.GetEventDefinition(eh);
        var acc = e.GetAccessors();
        accessors.Add(acc.Adder); accessors.Add(acc.Remover);
        if (!acc.Adder.IsNil && Open(md.GetMethodDefinition(acc.Adder).Attributes))
            lines.Add($"{full} | event {TypeName(e.Type)} {md.GetString(e.Name)}");
    }
    foreach (var fh in t.GetFields())
    {
        var f = md.GetFieldDefinition(fh);
        string n = md.GetString(f.Name);
        if (!OpenF(f.Attributes) || Generated(n) || n == "value__") continue;
        if (kind == "enum") { lines.Add($"{full} | value {n} = {Const(f)}"); continue; }
        string m = (f.Attributes & FieldAttributes.Literal) != 0 ? $"const " : ((f.Attributes & FieldAttributes.Static) != 0 ? "static " : "") + ((f.Attributes & FieldAttributes.InitOnly) != 0 ? "readonly " : "");
        string val = (f.Attributes & FieldAttributes.Literal) != 0 ? " = " + Const(f) : "";
        lines.Add($"{full} | {m}field {f.DecodeSignature(prov, null)} {n}{val}");
    }
    foreach (var mh in t.GetMethods())
    {
        if (accessors.Contains(mh)) continue;
        var m = md.GetMethodDefinition(mh);
        string n = md.GetString(m.Name);
        if (!Open(m.Attributes) || Generated(n) || n == ".cctor") continue;
        if (kind == "delegate" && n != "Invoke") continue;
        var sig = m.DecodeSignature(prov, null);
        var names = m.GetParameters().Select(ph => md.GetParameter(ph)).Where(p => p.SequenceNumber > 0).OrderBy(p => p.SequenceNumber).Select(p => md.GetString(p.Name)).ToList();
        var ps = sig.ParameterTypes.Select((pt, i) => pt + (i < names.Count ? " " + names[i] : "")).ToList();
        string st = (m.Attributes & MethodAttributes.Static) != 0 ? "static " : (m.Attributes & MethodAttributes.Abstract) != 0 ? "abstract " : (m.Attributes & MethodAttributes.Virtual) != 0 && (m.Attributes & MethodAttributes.NewSlot) != 0 && kind == "class" ? "virtual " : (m.Attributes & MethodAttributes.Virtual) != 0 && kind == "class" ? "override " : "";
        string generic = m.GetGenericParameters().Count > 0 ? "<" + string.Join(",", m.GetGenericParameters().Select(g => md.GetString(md.GetGenericParameter(g).Name))) + ">" : "";
        lines.Add(n == ".ctor" ? $"{full} | {Acc(m.Attributes)} ctor({string.Join(", ", ps)})" : $"{full} | {Acc(m.Attributes)} {st}{sig.ReturnType} {n}{generic}({string.Join(", ", ps)})");
    }
}
lines.Sort(StringComparer.Ordinal);

if (args.Length == 1) { foreach (var l in lines) Console.WriteLine(l); return 0; }
string baseline = args[2];
if (args[1] == "write") { File.WriteAllLines(baseline, lines); Console.WriteLine($"{lines.Count} lines written to {baseline}"); return 0; }
if (args[1] != "check") { Console.Error.WriteLine("second argument: check or write"); return 2; }
var want = File.Exists(baseline) ? File.ReadAllLines(baseline).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList() : new List<string>();
var added = lines.Except(want).ToList();
var removed = want.Except(lines).ToList();
foreach (var l in removed) Console.WriteLine("- " + l);
foreach (var l in added) Console.WriteLine("+ " + l);
Console.WriteLine(added.Count + removed.Count == 0 ? $"public surface matches {Path.GetFileName(baseline)} ({lines.Count} lines)"
    : $"public surface differs from {Path.GetFileName(baseline)}: {removed.Count} line(s) gone, {added.Count} new");
return added.Count + removed.Count == 0 ? 0 : 1;

string BaseOf(TypeDefinition t) => TypeName(t.BaseType);
string TypeName(EntityHandle h) => h.Kind switch
{
    HandleKind.TypeDefinition => Full(md.GetTypeDefinition((TypeDefinitionHandle)h)),
    HandleKind.TypeReference => RefName((TypeReferenceHandle)h),
    HandleKind.TypeSpecification => md.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(prov, null),
    _ => h.Kind.ToString(),
};
string RefName(TypeReferenceHandle h) { var r = md.GetTypeReference(h); string ns = md.GetString(r.Namespace); return (ns.Length > 0 ? ns + "." : "") + md.GetString(r.Name); }
string Const(FieldDefinition f)
{
    var c = f.GetDefaultValue();
    if (c.IsNil) return "?";
    var cc = md.GetConstant(c);
    var br = md.GetBlobReader(cc.Value);
    return cc.TypeCode switch
    {
        ConstantTypeCode.String => "\"" + br.ReadUTF16(br.Length) + "\"",
        ConstantTypeCode.Int32 => br.ReadInt32().ToString(),
        ConstantTypeCode.Int64 => br.ReadInt64().ToString(),
        ConstantTypeCode.Byte => br.ReadByte().ToString(),
        ConstantTypeCode.Boolean => br.ReadBoolean().ToString(),
        ConstantTypeCode.Single => br.ReadSingle().ToString(System.Globalization.CultureInfo.InvariantCulture),
        ConstantTypeCode.Double => br.ReadDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => cc.TypeCode.ToString(),
    };
}

sealed class Prov : ISignatureTypeProvider<string, object>
{
    public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString();
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) { var t = r.GetTypeDefinition(h); string ns = r.GetString(t.Namespace); return t.GetDeclaringType().IsNil ? (ns.Length > 0 ? ns + "." : "") + r.GetString(t.Name) : GetTypeFromDefinition(r, t.GetDeclaringType(), k) + "+" + r.GetString(t.Name); }
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) { var t = r.GetTypeReference(h); string ns = r.GetString(t.Namespace); return (ns.Length > 0 ? ns + "." : "") + r.GetString(t.Name); }
    public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte k) => r.GetTypeSpecification(h).DecodeSignature(this, c);
    public string GetSZArrayType(string e) => e + "[]";
    public string GetArrayType(string e, ArrayShape s) => e + "[" + new string(',', s.Rank - 1) + "]";
    public string GetByReferenceType(string e) => "ref " + e;
    public string GetPointerType(string e) => e + "*";
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g + "<" + string.Join(",", a) + ">";
    public string GetGenericTypeParameter(object c, int i) => "T" + i;
    public string GetGenericMethodParameter(object c, int i) => "M" + i;
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    public string GetModifiedType(string m, string u, bool req) => u;
    public string GetPinnedType(string e) => e;
}
