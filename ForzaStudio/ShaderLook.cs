using System;
using System.Collections.Generic;

namespace ForzaStudio
{
    // Simple reading of the game's car shaders for the viewport (not a translation: the fxobj files are compiled
    // Xbox 360 microcode). A material is a technique name; the classes below come from what its pixel shaders
    // declare (constants such as PaintColor / GlassColor / ChromeDiffuseScale, samplers such as LightsSampler),
    // see ShaderLookTable for the result on every FM4 / FH1 / FH2 / FM3 technique and Look() for the BasicEffect values.
    public enum ShaderKind
    {
        Generic,    // textured, nothing special
        Paint,      // car paint: body colour + metallic highlight
        Chrome,     // mirror metal: dark diffuse, strong white highlight
        Metal,      // brushed / anodized / bump metal
        Glass,      // transparent, tinted
        Light,      // lamp lens: texture + a bit of glow
        Emissive,   // gauges, LEDs, lit parts
        Carbon,     // carbon fiber
        Rubber,     // black rubber, plastic trim, undercarriage
        Soft,       // cockpit leather, cloth, suede, wood, plastic: matte
        Ghost,      // fading placeholder (hidden car parts)
        Pass,       // engine passes (shadow, depth, debug): nothing to see
        Missing     // no such technique in FM4: magenta
    }

    public struct ShaderLookValues
    {
        public ShaderKind Kind;
        public Vector3Lite Diffuse;   // multiplied with the texture / base grey
        public Vector3Lite Emissive;
        public Vector3Lite Specular;
        public float SpecularPower;
        public float Alpha;
        public bool UseTexture;       // the diffuse comes from the texture (else the base grey / the paint colour)
    }

    // Plain float triple so the table and the harnesses do not need XNA.
    public struct Vector3Lite
    {
        public float X, Y, Z;
        public Vector3Lite(float x, float y, float z) { this.X = x; this.Y = y; this.Z = z; }
        public Vector3Lite(float v) { this.X = v; this.Y = v; this.Z = v; }
    }

    public static partial class ShaderLook
    {
        // Techniques whose class needs more than the name: read from the pixel shaders of FM4 / FH1 / FH2 / FM3
        // (the 36 of 391 where the shader constants and the name rules disagree).
        private static readonly Dictionary<string, ShaderKind> Known = new Dictionary<string, ShaderKind>(StringComparer.OrdinalIgnoreCase)
        {
            { "badge2", ShaderKind.Paint }, { "brinl", ShaderKind.Light }, { "brinr", ShaderKind.Light }, { "brkreverse", ShaderKind.Light },
            { "cloth", ShaderKind.Paint }, { "cloth2", ShaderKind.Paint }, { "engine", ShaderKind.Paint }, { "engine_embossed_CF", ShaderKind.Carbon },
            { "hidhead", ShaderKind.Light }, { "lights_chrome", ShaderKind.Light }, { "lights_metal", ShaderKind.Light }, { "lights_plastic", ShaderKind.Light },
            { "lights_gls_hidhead", ShaderKind.Glass }, { "lights_gls_indicator_left", ShaderKind.Glass }, { "lights_gls_indicator_right", ShaderKind.Glass },
            { "lights_gls_noemit", ShaderKind.Glass }, { "lights_gls_oldhead", ShaderKind.Glass }, { "lights_gls_reverse_light", ShaderKind.Glass },
            { "lights_gls_tail_light", ShaderKind.Glass }, { "lights_gls_taillight2S", ShaderKind.Glass }, { "lights_gls_taillightst", ShaderKind.Glass },
            { "lindtailside", ShaderKind.Light }, { "linred", ShaderKind.Light }, { "liveryBadge", ShaderKind.Paint }, { "misc", ShaderKind.Paint },
            { "mottled", ShaderKind.Paint }, { "oldhead", ShaderKind.Light }, { "race_kit_red", ShaderKind.Paint }, { "rindtailside", ShaderKind.Light },
            { "rinred", ShaderKind.Light }, { "slinlorange", ShaderKind.Light }, { "slinrorange", ShaderKind.Light }, { "slorange", ShaderKind.Light },
            { "stripe_badge", ShaderKind.Paint }, { "wheel_black", ShaderKind.Rubber }, { "xenonhead", ShaderKind.Light }
        };

        private static Dictionary<string, bool> MakeSet(string[] names)
        {
            Dictionary<string, bool> set = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string name in names) set[name] = true;
            return set;
        }

        // Class of a material (= technique) name as the carbin gives it. Whether FM4 really uses the material is not
        // decided here: the compiled techniques are no test (cockpit files of FM4 cars use names such as `window` or
        // `plastic2` that have no cockpit_ technique, and lights_amber has one but renders black); the viewport
        // paints a mesh magenta from ForzaMesh.Unused, set from the lists of what FM4 cars use (Fm4MaterialUse).
        public static ShaderKind For(string technique)
        {
            ShaderKind kind;
            if (string.IsNullOrEmpty(technique)) return ShaderKind.Generic;
            if (Known.TryGetValue(technique, out kind)) return kind;
            return Classify(technique, null);
        }

        // Class from the technique name and what its pixel shaders declare (constants + samplers, names only).
        public static ShaderKind Classify(string technique, ICollection<string> features)
        {
            string n = (technique ?? "").ToLowerInvariant();
            if (n.StartsWith("ghost") || n.StartsWith("cockpit_ghost") || Has(features, "GhostColor") || Has(features, "GhostInteriorColor")) return ShaderKind.Ghost;
            if (n == "zonly" || n == "missing" || (n.Contains("shadow") && !n.Contains("unshadowed")) || n.Contains("smask") || n.StartsWith("debug") || n.Contains("missingbrake")
                || n == "stencil" || n == "stencilraw" || n == "drawlines" || n == "wireframe" || n == "default" || n == "scaling_text") return ShaderKind.Pass;
            // lights_* are the light-aware variants of the usual materials (lights_metal = metal ...); only amber / red are lamps themselves
            if (n.StartsWith("lights_") && n != "lights_amber" && n != "lights_red" && !n.StartsWith("lights_gls_") && n != "lights_glass") return Classify(n.Substring(7), features);
            if (n.Contains("plate")) return ShaderKind.Generic;
            if (n.Contains("glass") || n.Contains("window") || n.Contains("windshield") || Has(features, "GlassColor") || Has(features, "S:DamageGlassSampler")) return ShaderKind.Glass;
            if (n.Contains("gauge") || n.Contains("emissive") || n.Contains("_led") || n.Contains("glow") || Has(features, "S:CockpitGaugeEmissiveSampler")) return ShaderKind.Emissive;
            if (n.Contains("light") || n.Contains("lamp") || n.Contains("fullbeam") || n.Contains("drl") || n.Contains("indicator") || n.Contains("fog") || n.Contains("reflector") || n.Contains("lens")
                || Has(features, "S:LightsSampler") || Has(features, "S:HeadlightSampler") || Has(features, "BrakeColor")) return ShaderKind.Light;
            if (n.Contains("chrome") || n.Contains("mirror") || Has(features, "ChromeDiffuseScale")) return ShaderKind.Chrome;
            if (n.Contains("carbon") || Any(features, "CarbonFiber")) return ShaderKind.Carbon;
            if (n.Contains("rubber") || n.Contains("tire") || n.Contains("tyre") || n.Contains("weatherstrip") || n == "black" || n.Contains("undercarriage") || n.Contains("bottom") || n.Contains("cage") || Has(features, "BlackColor")) return ShaderKind.Rubber;
            // the full body pixel shader declares the two-tone / stripe paint constants; misc, engine, cloth ... only share its metal part
            if (n.Contains("paint") || n.Contains("body") || (Has(features, "PaintColor") && Has(features, "MetalColor") && Has(features, "TwoToneColor"))) return ShaderKind.Paint;
            if (n.Contains("metal") || n.Contains("alumin") || n.Contains("steel") || n.Contains("rim") || n.Contains("caliper") || n.Contains("rotor") || n.Contains("badge") || n.Contains("emblem") || n.Contains("grille")) return ShaderKind.Metal;
            if (n.StartsWith("cockpit_") || Has(features, "S:CockpitInteriorHiSampler") || Any(features, "Cockpit")) return ShaderKind.Soft;
            return ShaderKind.Generic;
        }

        private static bool Has(ICollection<string> features, string f)
        {
            return features != null && features.Contains(f);
        }

        private static bool Any(ICollection<string> features, string part)
        {
            if (features == null) return false;
            foreach (string f in features) if (f.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // How much of the environment picture a class mirrors (0 = none), used by the viewport's EnvReflection pass.
        public static float Reflectivity(ShaderKind kind)
        {
            switch (kind)
            {
                case ShaderKind.Paint: return 0.35f;
                case ShaderKind.Chrome: return 0.9f;
                case ShaderKind.Metal: return 0.6f;
                case ShaderKind.Glass: return 0.5f;
                case ShaderKind.Light: return 0.3f;
                case ShaderKind.Carbon: return 0.3f;
                case ShaderKind.Generic: return 0.1f;
                case ShaderKind.Rubber: return 0.05f;
                case ShaderKind.Soft: return 0.03f;
                default: return 0f;
            }
        }

        // BasicEffect-style values for a class. `paint` is the body colour (null = the viewer's base grey).
        public static ShaderLookValues Look(ShaderKind kind)
        {
            ShaderLookValues v = new ShaderLookValues { Kind = kind, Diffuse = new Vector3Lite(1f), Emissive = new Vector3Lite(0f), Specular = new Vector3Lite(0.3f), SpecularPower = 20f, Alpha = 1f, UseTexture = true };
            switch (kind)
            {
                case ShaderKind.Paint:    v.Specular = new Vector3Lite(0.7f); v.SpecularPower = 60f; v.UseTexture = false; break;
                case ShaderKind.Chrome:   v.Diffuse = new Vector3Lite(0.25f); v.Specular = new Vector3Lite(1f); v.SpecularPower = 90f; v.UseTexture = false; break;
                case ShaderKind.Metal:    v.Diffuse = new Vector3Lite(0.65f); v.Specular = new Vector3Lite(0.8f); v.SpecularPower = 40f; break;
                case ShaderKind.Glass:    v.Diffuse = new Vector3Lite(0.15f); v.Specular = new Vector3Lite(1f); v.SpecularPower = 120f; v.Alpha = 0.28f; v.UseTexture = false; break;
                case ShaderKind.Light:    v.Emissive = new Vector3Lite(0.22f); v.Specular = new Vector3Lite(0.9f); v.SpecularPower = 80f; break;
                case ShaderKind.Emissive: v.Emissive = new Vector3Lite(0.9f); v.Specular = new Vector3Lite(0.1f); break;
                case ShaderKind.Carbon:   v.Diffuse = new Vector3Lite(0.55f); v.Specular = new Vector3Lite(0.55f); v.SpecularPower = 35f; break;
                case ShaderKind.Rubber:   v.Diffuse = new Vector3Lite(0.12f); v.Specular = new Vector3Lite(0.08f); v.SpecularPower = 8f; v.UseTexture = false; break;
                case ShaderKind.Soft:     v.Specular = new Vector3Lite(0.1f); v.SpecularPower = 8f; break;
                case ShaderKind.Ghost:    v.Alpha = 0.12f; v.Specular = new Vector3Lite(0f); v.UseTexture = false; break;
                case ShaderKind.Pass:     v.Alpha = 0f; break;
                case ShaderKind.Missing:  v.Diffuse = new Vector3Lite(0f); v.Emissive = new Vector3Lite(1f, 0f, 1f); v.Specular = new Vector3Lite(0f); v.UseTexture = false; break;
            }
            return v;
        }
    }
}
