// metadump <assembly> [--props-of-type <substring>] <type-name prefix>...
//   Lists base type, enum values, properties (with types) and methods (with signatures) of every type whose full name starts
//   with one of the prefixes ("" = all types). Il2CppInterop plumbing (NativeFieldInfoPtr_*, __rpc*) is skipped.
//   --props-of-type X: instead, list every property whose type contains X, as "Type.prop : type".
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var argList = args.ToList();
string propsOfType = null;
int pi = argList.IndexOf("--props-of-type");
if (pi >= 0) { propsOfType = argList[pi + 1]; argList.RemoveRange(pi, 2); }
using var pe = new PEReader(File.OpenRead(argList[0]));
var md = pe.GetMetadataReader();
var prov = new Prov();
var prefixes = argList.Skip(1).ToList();
if (prefixes.Count == 0) prefixes.Add("");

string BaseName(EntityHandle h) => h.IsNil ? "-" : h.Kind switch
{
    HandleKind.TypeDefinition => Full(md.GetTypeDefinition((TypeDefinitionHandle)h)),
    HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Namespace) + "." + md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Name),
    _ => h.Kind.ToString(),
};
string Full(TypeDefinition t)
{
    string ns = md.GetString(t.Namespace), n = md.GetString(t.Name);
    if (!t.GetDeclaringType().IsNil) return Full(md.GetTypeDefinition(t.GetDeclaringType())) + "+" + n;
    return ns.Length > 0 ? ns + "." + n : n;
}
bool Plumbing(string n) => n.StartsWith("NativeFieldInfoPtr") || n.StartsWith("NativeMethodInfoPtr") || n.StartsWith("__") || n.Contains("_b__");

foreach (var th in md.TypeDefinitions)
{
    var t = md.GetTypeDefinition(th);
    string full = Full(t);
    if (!prefixes.Any(p => full.StartsWith(p))) continue;
    if (propsOfType != null)
    {
        foreach (var ph in t.GetProperties())
        {
            var p = md.GetPropertyDefinition(ph);
            string type = p.DecodeSignature(prov, null).ReturnType;
            if (type.Contains(propsOfType)) Console.WriteLine($"{full}.{md.GetString(p.Name)} : {type}");
        }
        continue;
    }
    Console.WriteLine($"== {full} : {BaseName(t.BaseType)}");
    if (BaseName(t.BaseType) == "System.Enum")
        foreach (var fh in t.GetFields())
        {
            var f = md.GetFieldDefinition(fh);
            if (f.GetDefaultValue().IsNil) continue;
            var blob = md.GetBlobReader(md.GetConstant(f.GetDefaultValue()).Value);
            Console.WriteLine($"  value {md.GetString(f.Name)} = {(blob.Length == 4 ? blob.ReadInt32() : blob.Length == 2 ? blob.ReadInt16() : blob.ReadByte())}");
        }
    foreach (var ph in t.GetProperties())
    {
        var p = md.GetPropertyDefinition(ph);
        string n = md.GetString(p.Name);
        if (!Plumbing(n)) Console.WriteLine($"  prop {n} : {p.DecodeSignature(prov, null).ReturnType}");
    }
    foreach (var mh in t.GetMethods())
    {
        var m = md.GetMethodDefinition(mh);
        string n = md.GetString(m.Name);
        if (n.StartsWith("get_") || n.StartsWith("set_") || n == ".cctor" || Plumbing(n)) continue;
        var sig = m.DecodeSignature(prov, null);
        Console.WriteLine($"  meth {sig.ReturnType} {n}({string.Join(", ", sig.ParameterTypes)})");
    }
}

sealed class Prov : ISignatureTypeProvider<string, object>
{
    public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString();
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => r.GetString(r.GetTypeDefinition(h).Name);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) { var t = r.GetTypeReference(h); return r.GetString(t.Namespace) + "." + r.GetString(t.Name); }
    public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte k) => "spec";
    public string GetSZArrayType(string e) => e + "[]";
    public string GetArrayType(string e, ArrayShape s) => e + "[,]";
    public string GetByReferenceType(string e) => "ref " + e;
    public string GetPointerType(string e) => e + "*";
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g + "<" + string.Join(",", a) + ">";
    public string GetGenericTypeParameter(object c, int i) => "T" + i;
    public string GetGenericMethodParameter(object c, int i) => "M" + i;
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    public string GetModifiedType(string m, string u, bool req) => u;
    public string GetPinnedType(string e) => e;
}
