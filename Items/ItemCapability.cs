using Il2Cpp;
using Il2CppItems;
using MelonLoader;
using POGLib.Core;
using UnityEngine;

namespace POGLib.Items
{
    /// <summary>
    /// A declarative item capability (EXPERIMENTAL, v0.2). In this game an "effect" (eat / weapon /
    /// throw / …) is NOT a subclass of InventoryItem — it is a sibling <c>ActiveItem_*</c> /
    /// <c>PassiveItem_*</c> component on the same GameObject (see _research/ITEM_ANATOMY.md §2). This
    /// API lets a content pack DECLARE such a capability on a <see cref="ModItemDefinition"/>; the Lib
    /// attaches the component and sets the SCALAR fields it knows are safe.
    ///
    /// RUNTIME-TODO (Milestone 0): a bare shell has none of the serialized references these components
    /// normally ship with (VFX, sounds, curves, and NetworkVariables that init at spawn). The scalar
    /// effect logic should work; missing refs may warn or no-op. Attaching NetworkBehaviour components
    /// also changes the NetworkObject's behaviour list — every peer builds identically (same def, same
    /// order) so it should stay in sync under ForceSamePrefabs, but confirm in the 2-player test.
    /// </summary>
    public abstract class ItemCapability
    {
        /// <summary>Capability name, for logs.</summary>
        public abstract string Name { get; }

        /// <summary>
        /// Attach and configure the capability on the item. Called on host AND client at build time,
        /// at the same point in the same order, so every peer ends up with an identical object.
        /// PUBLIC on purpose: this is the extension seam — a third-party mod overrides this to add
        /// its own capability, and the built-in ones use exactly the same contract (no privileged core).
        /// </summary>
        public abstract void Attach(InventoryItem item);

        /// <summary>Get the capability component, adding it if absent.</summary>
        protected static T AddOrGet<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }

    /// <summary>
    /// Base class for a VISUAL effect on an item — glow, sparks, lightning, frost, smoke, anything.
    /// Subclass it in your own mod to ship an effect the Lib has never heard of:
    ///
    /// <code>
    /// public sealed class LightningEffect : ItemVisualEffect
    /// {
    ///     public override string Name => "Lightning";
    ///     public override void Attach(InventoryItem item)
    ///     {
    ///         var host = CreateEffectChild(item, "Lightning");   // child object, cleaned up on rebuild
    ///         // build the effect: clone a vanilla VFX, instantiate your own bundle prefab,
    ///         // add a Light, a TrailRenderer, an Animator — whatever Unity can do.
    ///     }
    /// }
    /// </code>
    ///
    /// Then just declare it: <c>Capabilities = { new LightningEffect() }</c>. Nothing about visual
    /// effects is hardcoded — <see cref="GlowCapability"/> and <see cref="ParticleEffect"/> are only
    /// convenience implementations of this same public contract.
    /// </summary>
    public abstract class ItemVisualEffect : ItemCapability
    {
        /// <summary>Prefix for effect child objects, so rebuilds replace rather than stack them.</summary>
        public const string ChildPrefix = "ModEffect_";

        /// <summary>
        /// Create (or reuse) a child GameObject to host this effect. Reusing by name means rebuilding
        /// an item never leaves duplicate effects behind.
        /// </summary>
        protected static GameObject CreateEffectChild(InventoryItem item, string effectName,
                                                      Vector3 localOffset = default)
        {
            string childName = ChildPrefix + effectName;
            var existing = item.transform.Find(childName);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(childName);
            go.transform.SetParent(item.transform, false);
            go.transform.localPosition = localOffset;
            return go;
        }

        /// <summary>
        /// The item's visible mesh root — the reskin/bundle visual if there is one, else the item
        /// itself. Parent effects here when they should follow the model rather than the shell.
        /// </summary>
        protected static Transform GetVisualRoot(InventoryItem item)
        {
            var visual = item.transform.Find(GameNames.ModVisualChild);
            return visual != null ? visual : item.transform;
        }

        /// <summary>
        /// Restart every ModEffect_* child on a LIVE item instance. Needed because effects are built
        /// on the template, which is kept inactive — ParticleSystem.Play() there is a silent no-op, so
        /// a freshly spawned clone would sit with its particles stopped. Called from the Lib's
        /// OnNetworkSpawn patch on every mod item instance (host and clients alike).
        /// </summary>
        internal static void RestartEffectsOn(InventoryItem item)
        {
            if (item == null) return;
            foreach (var t in item.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.name.StartsWith(ChildPrefix)) continue;
                t.gameObject.SetActive(true);
                foreach (var ps in t.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps == null) continue;
                    ps.gameObject.SetActive(true);
                    try { ps.Play(true); } catch { /* best-effort */ }
                }
            }
        }

        /// <summary>Whether this object carries any effect children (cheap one-off check).</summary>
        internal static bool HasEffects(GameObject root)
        {
            if (root == null) return false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.StartsWith(ChildPrefix)) return true;
            return false;
        }

        /// <summary>
        /// Show/hide every effect child. Used to stop an item's aura from playing while it is stowed
        /// in the inventory — the game's own hide logic only knows about ITS renderer lists, not ours.
        /// </summary>
        internal static void SetEffectsVisible(GameObject root, bool visible)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.name.StartsWith(ChildPrefix)) continue;
                t.gameObject.SetActive(visible);
                if (!visible) continue;
                foreach (var ps in t.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps == null) continue;
                    try { ps.Play(true); } catch { /* best-effort */ }
                }
            }
        }
    }

    /// <summary>
    /// Makes an item USABLE — i.e. something the game is willing to run a primary action on.
    ///
    /// The vanilla input action is <c>UseItem</c> (LMB), and it ends at
    /// <c>ActiveItem_ReadyItem.OnStartPrimaryAction</c>. A bare shell such as Item_Diamond carries no
    /// <c>ActiveItem_*</c> component at all, so a mod item cloned from it has nothing on the receiving
    /// end of that chain. This attaches the game's own hold-to-use base, with every serialized field
    /// it would normally ship with filled in, so the item behaves like a usable one.
    ///
    /// Attached automatically for any definition that declares a <c>UseHandlerId</c> — a use handler
    /// with no way to be triggered is never what the author meant.
    /// </summary>
    public sealed class UsableCapability : ItemVisualEffect
    {
        /// <summary>Seconds of "readying" before the effect fires. 0 = instant on click.</summary>
        public float ReadyDuration = 0f;
        /// <summary>Field-of-view change while readying (0 = none).</summary>
        public float FovChange = 0f;
        /// <summary>Show the crosshair while holding the item.</summary>
        public bool UseCrosshair = false;

        public override string Name => "ActiveItem_ReadyItem (usable)";

        public override void Attach(InventoryItem item)
        {
            var ready = AddOrGet<ActiveItem_ReadyItem>(item.gameObject);
            ReadyItemFields.Fill(ready, item, null);
            ready.m_readyItemDuration = ReadyDuration;
            ready.m_fovChange = FovChange;
            ready.m_useCrosshair = UseCrosshair;
        }
    }

    /// <summary>
    /// The serialized fields of ActiveItem_ReadyItem (the base of every hold-to-use item, food included) that an
    /// added component does not get from a prefab. Left null they throw: m_endPositionTransform in OnNetworkSpawn on
    /// the 2026-11-14 build (a shop-spawned food item, seen in the Unity log), and in Update before that.
    /// </summary>
    internal static class ReadyItemFields
    {
        /// <summary>Fill <paramref name="ready"/>; with a <paramref name="donor"/>, its timing and its end pose.</summary>
        public static void Fill(ActiveItem_ReadyItem ready, InventoryItem item, ActiveItem_ReadyItem donor)
        {
            ready.m_inventoryItem = item;
            ready.m_readyItemDuration = donor != null ? donor.m_readyItemDuration : 0f;
            ready.m_readyItemCurve = donor?.m_readyItemCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            ready.m_effectTime = donor != null ? donor.m_effectTime : 0f;
            ready.m_fovChange = donor != null ? donor.m_fovChange : 0f;
            ready.m_fovChangeReadyFactor = donor != null ? donor.m_fovChangeReadyFactor : 1f;
            ready.m_useCrosshair = donor != null && donor.m_useCrosshair;
            ready.m_useCrosshairReadyOnly = donor != null && donor.m_useCrosshairReadyOnly;
            ready.m_preventUseIfExhausted = donor != null && donor.m_preventUseIfExhausted;
            if (ready.m_endPositionTransform == null)
                ready.m_endPositionTransform = EndPoint(item, donor);
        }

        // The donor's end point is a transform of its own prefab; give the item one at the same local pose.
        private static Transform EndPoint(InventoryItem item, ActiveItem_ReadyItem donor)
        {
            var end = donor?.m_endPositionTransform;
            if (end == null || end == donor.transform) return item.transform;
            var go = new GameObject("POG_ReadyEnd");
            go.transform.SetParent(item.transform, false);
            go.transform.localPosition = donor.transform.InverseTransformPoint(end.position);
            go.transform.localRotation = Quaternion.Inverse(donor.transform.rotation) * end.rotation;
            return go.transform;
        }
    }

    /// <summary>
    /// Edible (component <c>ActiveItem_Eat</c>). Since the 2026-11-06 game update food no longer fills a
    /// hunger meter: it heals, refills stamina, raises the character's stats (Body / Stamina / Strength,
    /// see <c>PlayerStats</c>) and can apply a buff.
    ///
    /// Not every vanilla food uses this component: bread is ActiveItem_Bread (it leaves crumbs), drinks are
    /// ActiveItem_Drink. Plain foods such as Item_Apple, Item_Orange, Item_Cheese and Item_Meat use it.
    ///
    /// Every setting is optional. <see cref="CopyFrom"/> starts from a vanilla food item — all of its
    /// eat settings, including its buff and eating VFX, which a bare shell does not have — and the values
    /// you set override it. Without <see cref="CopyFrom"/>, unset values stay at the component's defaults
    /// (zero / off), which is what earlier versions of this class did with its zero defaults.
    /// </summary>
    public sealed class EatCapability : ItemCapability
    {
        /// <summary>Vanilla food prefab to start from, e.g. "Item_Mushroom_Normal" (null = start empty).</summary>
        public string CopyFrom;

        public float? HealthOnEating;
        public float? DamageOnEating;
        public float? StaminaRefill;
        public float? MinStaminaBonus;
        public float? MaxStaminaBonus;
        public float? StomachFullTime;
        public bool? UseStaminaMin;

        /// <summary>Points added to the Body stat (more max health) when eaten.</summary>
        public int? BodyValue;
        /// <summary>Points added to the Stamina stat when eaten (not the same as <see cref="StaminaRefill"/>).</summary>
        public int? StaminaValue;
        /// <summary>Points added to the Strength stat (more damage) when eaten.</summary>
        public int? StrengthValue;

        /// <summary>
        /// Apply the buff of this vanilla food item, e.g. "Item_Mushroom_Glowing" (the buff object lives on
        /// that item's ActiveItem_Eat). Overrides the buff taken from <see cref="CopyFrom"/>.
        /// </summary>
        public string BuffFrom;
        public float? BuffValueToAdd;
        public float? BuffValueToStart;

        public override string Name => "ActiveItem_Eat";

        public override void Attach(InventoryItem item)
        {
            var eat = AddOrGet<ActiveItem_Eat>(item.gameObject);

            ActiveItem_Eat donor = null;
            if (!string.IsNullOrEmpty(CopyFrom))
            {
                donor = FindVanillaEat(CopyFrom);
                if (donor != null) CopySettings(donor, eat);
                else MelonLogger.Warning($"[POGLib] EatCapability: CopyFrom '{CopyFrom}' unavailable; starting empty.");
            }
            // ActiveItem_Eat is an ActiveItem_ReadyItem: its base fields need filling like any added one.
            ReadyItemFields.Fill(eat, item, donor);

            if (HealthOnEating.HasValue) eat.m_healthOnEating = HealthOnEating.Value;
            if (DamageOnEating.HasValue) eat.m_damageOnEating = DamageOnEating.Value;
            if (StaminaRefill.HasValue) eat.m_staminaRefill = StaminaRefill.Value;
            if (MinStaminaBonus.HasValue) eat.m_minStaminaBonus = MinStaminaBonus.Value;
            if (MaxStaminaBonus.HasValue) eat.m_maxStaminaBonus = MaxStaminaBonus.Value;
            if (StomachFullTime.HasValue) eat.m_stomachFullTime = StomachFullTime.Value;
            if (UseStaminaMin.HasValue) eat.m_useStaminaMin = UseStaminaMin.Value;
            if (BodyValue.HasValue) eat.m_bodyValue = BodyValue.Value;
            if (StaminaValue.HasValue) eat.m_staminaValue = StaminaValue.Value;
            if (StrengthValue.HasValue) eat.m_strengthValue = StrengthValue.Value;

            if (!string.IsNullOrEmpty(BuffFrom))
            {
                var buffDonor = FindVanillaEat(BuffFrom);
                if (buffDonor != null && buffDonor.m_applyBuff != null)
                {
                    eat.m_applyBuff = buffDonor.m_applyBuff;
                    eat.m_buffValueToAdd = buffDonor.m_buffValueToAdd;
                    eat.m_buffValueToStart = buffDonor.m_buffValueToStart;
                }
                else MelonLogger.Warning($"[POGLib] EatCapability: BuffFrom '{BuffFrom}' has no buff (or is not a loaded food item).");
            }
            if (BuffValueToAdd.HasValue) eat.m_buffValueToAdd = BuffValueToAdd.Value;
            if (BuffValueToStart.HasValue) eat.m_buffValueToStart = BuffValueToStart.Value;
        }

        private static ActiveItem_Eat FindVanillaEat(string prefabName)
        {
            var prefab = GameAssets.FindItemPrefab(prefabName);
            if (prefab == null) { MelonLogger.Warning($"[POGLib] EatCapability: no item prefab named '{prefabName}' is loaded yet."); return null; }
            var eat = prefab.GetComponentInChildren<ActiveItem_Eat>(true);
            if (eat == null) MelonLogger.Warning($"[POGLib] EatCapability: '{prefabName}' is loaded but has no ActiveItem_Eat (not a food item).");
            return eat;
        }

        private static void CopySettings(ActiveItem_Eat from, ActiveItem_Eat to)
        {
            to.m_healthOnEating = from.m_healthOnEating;
            to.m_damageOnEating = from.m_damageOnEating;
            to.m_staminaRefill = from.m_staminaRefill;
            to.m_minStaminaBonus = from.m_minStaminaBonus;
            to.m_maxStaminaBonus = from.m_maxStaminaBonus;
            to.m_stomachFullTime = from.m_stomachFullTime;
            to.m_useStaminaMin = from.m_useStaminaMin;
            to.m_bodyValue = from.m_bodyValue;
            to.m_staminaValue = from.m_staminaValue;
            to.m_strengthValue = from.m_strengthValue;
            to.m_applyBuff = from.m_applyBuff;
            to.m_buffValueToAdd = from.m_buffValueToAdd;
            to.m_buffValueToStart = from.m_buffValueToStart;
            to.m_eatVfx = from.m_eatVfx;
        }
    }

    /// <summary>Melee weapon: damages on hit and loses durability (component <c>ActiveItem_MeleeWeapon</c>).</summary>
    public sealed class MeleeWeaponCapability : ItemCapability
    {
        public int MaxDurability = 100;
        public int DurabilityLossOnEntityHit = 1;
        public int DurabilityLossOnDestructibleHit = 1;
        public float HitCooldown = 0.5f;

        public override string Name => "ActiveItem_MeleeWeapon";

        public override void Attach(InventoryItem item)
        {
            var w = AddOrGet<ActiveItem_MeleeWeapon>(item.gameObject);
            w.m_maxDurability = MaxDurability;
            w.m_durabilityLossOnEntityHit = DurabilityLossOnEntityHit;
            w.m_durabilityLossOnDestructibleHit = DurabilityLossOnDestructibleHit;
            w.m_meleeHitCooldown = HitCooldown;
            // m_currentDurability is a NetworkVariable<int> that initializes at spawn; seeding it to
            // MaxDurability belongs in an OnNetworkSpawn step, not here (RUNTIME-TODO). Hit VFX/sounds
            // are also unset on a bare shell.
        }
    }

    /// <summary>
    /// Glow / pulse (EXPERIMENTAL, v0.2). A glowing, pulsing item — the look the SpeakingStone has —
    /// is NOT a field on InventoryItem: it is a child <c>Light</c> plus emissive materials, animated
    /// by a pulse driver (item anatomy §9). This capability rebuilds that setup declaratively:
    ///
    ///   • a child GameObject with a <c>Light</c> (Colour / Intensity / Range) — the light half;
    ///   • an optional emissive tint on the item's renderers — the material half;
    ///   • an optional pulse via the game's own <c>LightFlicker</c> (DOTween: Strength / Duration /
    ///     Vibrato / Randomness), the same component the game uses for flickering lights.
    ///
    /// The DEFAULTS here are deliberately neutral, NOT copied from any vanilla item: the real numbers
    /// live in prefab serialized data and can only be read at runtime. Use <c>VisualProbe</c>
    /// (Content.Diagnostics.ProbeItem("Item_SpeakingStone")) to capture the actual values in-game and
    /// then set them explicitly here.
    ///
    /// RUNTIME-TODO (Milestone 0): under HDRP, <c>Light.intensity</c> is not the photometric intensity
    /// (HDAdditionalLightData owns lumens/lux), so a light created this way may read dimmer/brighter
    /// than a vanilla one — the probe reports both so the offset can be calibrated.
    /// </summary>
    public sealed class GlowCapability : ItemVisualEffect
    {
        /// <summary>Glow colour.</summary>
        public Color Colour = Color.white;
        /// <summary>
        /// Emissive brightness multiplier. HDRP treats emissive colour as HDR, so values above 1 make
        /// the surface read as lit in a dark room. ~2 is "clearly visible", ~6 is "blinding".
        /// </summary>
        public float Intensity = 2f;

        /// <summary>
        /// Opt-in real point light. OFF by default: a Light illuminates the whole room like a lantern,
        /// which is almost never what "make my item glow" means — the default is a self-lit surface
        /// that is visible in the dark without lighting anything else.
        /// </summary>
        public bool CastsLight = false;
        /// <summary>Point-light intensity (only when <see cref="CastsLight"/>).</summary>
        public float LightIntensity = 1f;
        /// <summary>Point-light range in metres (only when <see cref="CastsLight"/>).</summary>
        public float LightRange = 4f;
        /// <summary>Local offset of the light inside the item.</summary>
        public Vector3 LocalOffset = Vector3.zero;

        /// <summary>Emissive colour; falls back to <see cref="Colour"/> when unset.</summary>
        public Color? EmissiveColour = null;

        /// <summary>
        /// Restrict the glow to materials whose name contains this text (case-insensitive). Null or empty means every
        /// material on the item, which is right for a single-material reskin but wrong for a model with several: a
        /// lantern wants its shade lit, not its iron frame. An OBJ model's materials carry the names from the source
        /// file, so the filter is the name the material has in Blender. (Restored 2026-11-19 from the 2026-09 copy of
        /// the library, where POGCustomItems' Ember Lantern used it.)
        /// </summary>
        public string MaterialFilter = null;

        /// <summary>Pulse the light via the game's LightFlicker. Requires <see cref="CastsLight"/>.</summary>
        public bool Pulse = false;
        /// <summary>Pulse amplitude (LightFlicker.m_strength).</summary>
        public float PulseStrength = 1f;
        /// <summary>Pulse period in seconds (LightFlicker.m_duration).</summary>
        public float PulseDuration = 1f;
        /// <summary>Oscillations per pulse (LightFlicker.m_vibrato).</summary>
        public int PulseVibrato = 10;
        /// <summary>Pulse jitter 0..1 (LightFlicker.m_randomness).</summary>
        public float PulseRandomness = 0f;

        public override string Name => CastsLight ? "Glow (emissive + Light)" : "Glow (emissive)";

        public override void Attach(InventoryItem item)
        {
            var go = item.gameObject;

            // The default half: the surface itself glows. HDR emissive means "visible in the dark",
            // not "light source" — nothing around the item gets brighter.
            ApplyEmissive(go, (EmissiveColour ?? Colour) * Mathf.Max(0f, Intensity), MaterialFilter);

            if (!CastsLight) return;

            // Reuse our own light child if the item is rebuilt, so we never stack duplicates.
            var existing = go.transform.Find(GameNames.ModObjects.GlowLight);
            GameObject lightGo = existing != null
                ? existing.gameObject
                : new GameObject(GameNames.ModObjects.GlowLight);
            if (existing == null)
            {
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.localPosition = LocalOffset;
            }

            var light = AddOrGet<Light>(lightGo);
            light.type = LightType.Point;
            light.color = Colour;
            light.intensity = LightIntensity;
            light.range = LightRange;

            if (Pulse)
            {
                var flicker = AddOrGet<LightFlicker>(lightGo);
                flicker.m_light = light;
                flicker.m_strength = PulseStrength;
                flicker.m_duration = PulseDuration;
                flicker.m_vibrato = PulseVibrato;
                flicker.m_randomness = PulseRandomness;
            }
        }

        /// <summary>
        /// Set an emissive colour on the item's mesh renderers (material copies, HDRP first). Covers
        /// every mesh variant — world and in-hand — so the item glows the same however it is carried.
        /// </summary>
        private static void ApplyEmissive(GameObject root, Color colour, string materialFilter)
        {
            bool filtered = !string.IsNullOrEmpty(materialFilter);
            int lit = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r == null || r.gameObject.name.Contains("Outline")) continue;
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (filtered && mats[i].name.IndexOf(materialFilter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    lit++;
                    var copy = new Material(mats[i]);
                    if (copy.HasProperty(GameNames.Shader.EmissiveColor))
                        copy.SetColor(GameNames.Shader.EmissiveColor, colour);
                    else if (copy.HasProperty(GameNames.Shader.EmissionColor))
                    {
                        copy.SetColor(GameNames.Shader.EmissionColor, colour);
                        copy.EnableKeyword(GameNames.Shader.EmissionKeyword);
                    }
                    else continue;
                    mats[i] = copy;
                }
                r.materials = mats;
            }
            // A filter that matches nothing leaves the item looking untouched, with no clue why.
            if (filtered && lit == 0)
                MelonLogger.Warning($"[POGLib] GlowCapability: no material matched '{materialFilter}' on " +
                                    $"{root.name}: nothing glows. Check the material name in the model file.");
        }
    }

    /// <summary>
    /// A particle/VFX effect on an item — lightning, frost, smoke, sparks — WITHOUT writing an effect
    /// class. Two sources, both going through the same public <see cref="ItemVisualEffect"/> contract:
    ///
    ///   • <see cref="FromGameObject"/> — clone an existing VFX object from any loaded game prefab.
    ///     Costs nothing to ship and always matches the game's art style. Discover names with
    ///     <c>Content.Diagnostics.ListVfx("lightning")</c>.
    ///   • <see cref="FromBundle"/> — instantiate your own particle prefab from an AssetBundle
    ///     (particle systems survive IL2CPP; custom scripts do not — see CUSTOM_ASSETS.md).
    ///
    /// For anything these two cannot express, subclass <see cref="ItemVisualEffect"/> directly.
    /// </summary>
    public sealed class ParticleEffect : ItemVisualEffect
    {
        /// <summary>Effect id, also the child object name (so several effects can coexist).</summary>
        public string EffectId = "Particles";

        // — Source A: clone a VFX object out of a loaded game prefab —
        public string SourcePrefabName;   // e.g. "Item_SpeakingStone"
        public string SourceChildName;    // e.g. "VFX_SpeakingStone"

        // — Source B: your own particle prefab from a bundle —
        internal string BundlePath;
        internal string BundleAssetName;

        // — Placement —
        public Vector3 LocalOffset = Vector3.zero;
        public Vector3 LocalEuler = Vector3.zero;
        public float Scale = 1f;
        /// <summary>Parent to the item's visible model rather than the item root.</summary>
        public bool FollowVisualMesh = false;
        /// <summary>Recolour the particle material (null = leave the source colours).</summary>
        public Color? Tint = null;
        /// <summary>
        /// Start the particles immediately. Needed for most vanilla VFX: they ship stopped and are
        /// played by their owner's logic (e.g. the SpeakingStone's aura only plays while speaking),
        /// so a bare clone would sit there invisible.
        /// </summary>
        public bool PlayOnAttach = true;

        public override string Name => $"Particles({EffectId})";

        /// <summary>Clone a VFX child out of a loaded game prefab (no AssetBundle needed).</summary>
        public static ParticleEffect FromGameObject(string effectId, string sourcePrefabName, string sourceChildName)
            => new ParticleEffect { EffectId = effectId, SourcePrefabName = sourcePrefabName, SourceChildName = sourceChildName };

        /// <summary>Instantiate your own particle prefab from an AssetBundle.</summary>
        internal static ParticleEffect FromBundle(string effectId, string bundlePath, string assetName)
            => new ParticleEffect { EffectId = effectId, BundlePath = bundlePath, BundleAssetName = assetName };

        /// <summary>Position/rotate/scale the effect inside the item (fluent).</summary>
        public ParticleEffect At(Vector3 offset, Vector3 euler = default, float scale = 1f)
        {
            LocalOffset = offset; LocalEuler = euler; Scale = scale;
            return this;
        }

        /// <summary>Recolour the particles (fluent).</summary>
        public ParticleEffect Coloured(Color colour) { Tint = colour; return this; }

        public override void Attach(InventoryItem item)
        {
            GameObject source = ResolveSource();
            if (source == null)
            {
                MelonLogger.Warning($"[POGLib] ParticleEffect '{EffectId}': no source found " +
                                    $"(prefab='{SourcePrefabName}' child='{SourceChildName}' bundle='{BundlePath}').");
                return;
            }

            var host = CreateEffectChild(item, EffectId, LocalOffset);
            if (FollowVisualMesh) host.transform.SetParent(GetVisualRoot(item), false);

            // Replace any previous instance so a rebuild never stacks duplicates.
            for (int i = host.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(host.transform.GetChild(i).gameObject);

            var instance = UnityEngine.Object.Instantiate(source, host.transform);
            instance.name = EffectId;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localEulerAngles = LocalEuler;
            instance.transform.localScale = new Vector3(Scale, Scale, Scale);
            instance.SetActive(true);

            if (Tint.HasValue) TintRenderers(instance, Tint.Value);

            if (PlayOnAttach)
            {
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps == null) continue;
                    ps.gameObject.SetActive(true);
                    try { ps.Play(true); } catch { /* some systems refuse until spawned */ }
                }
            }
        }

        private GameObject ResolveSource()
        {
            if (!string.IsNullOrEmpty(BundlePath) && !string.IsNullOrEmpty(BundleAssetName))
            {
                var bundle = CoreServices.Assets.LoadBundle(BundlePath);
                return bundle != null ? CoreServices.Assets.LoadAsset<GameObject>(bundle, BundleAssetName) : null;
            }
            if (string.IsNullOrEmpty(SourcePrefabName) || string.IsNullOrEmpty(SourceChildName)) return null;

            // The donor may be an item prefab OR a standalone VFX prefab (e.g. an ItemBreak effect),
            // so fall back to a by-name GameObject lookup rather than only searching InventoryItems.
            var itemDonor = GameAssets.FindItemPrefab(SourcePrefabName);
            GameObject donorRoot = itemDonor != null ? itemDonor.gameObject : GameAssets.FindGameObject(SourcePrefabName);
            if (donorRoot == null) return null;

            foreach (var t in donorRoot.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.Contains(SourceChildName)) return t.gameObject;
            return null;
        }

        private static void TintRenderers(GameObject root, Color colour)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var copy = new Material(mats[i]);
                    if (copy.HasProperty(GameNames.Shader.BaseColor)) copy.SetColor(GameNames.Shader.BaseColor, colour);
                    else if (copy.HasProperty(GameNames.Shader.Color)) copy.SetColor(GameNames.Shader.Color, colour);
                    if (copy.HasProperty(GameNames.Shader.EmissiveColor)) copy.SetColor(GameNames.Shader.EmissiveColor, colour);
                    mats[i] = copy;
                }
                r.materials = mats;
            }
        }
    }

    /// <summary>Throwable: can be thrown for damage, has its own "health" (component <c>ActiveItem_Throwable</c>).</summary>
    public sealed class ThrowableCapability : ItemCapability
    {
        public int HealthMax = 1;
        public int HealthMin = 1;
        public float BounceFactor = 0f;

        public override string Name => "ActiveItem_Throwable";

        public override void Attach(InventoryItem item)
        {
            var t = AddOrGet<ActiveItem_Throwable>(item.gameObject);
            t.m_healthMax = HealthMax;
            t.m_healthMin = HealthMin;
            t.m_bounceFactor = BounceFactor;
            // m_damageType, hit VFX/sounds and the current-health NetworkVariable are left at defaults.
        }
    }
}
