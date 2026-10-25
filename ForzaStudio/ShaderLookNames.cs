using System;
using System.Collections.Generic;

namespace ForzaStudio
{
    public static partial class ShaderLook
    {
        // The 303 techniques of FM4 car shaders (Media\Shaders\Cars\*.fxobj: shaders_v16 + the single-material files);
        // any other material name has no shader in the game (fm4dev\Shaders\techniques_FM4.txt).
        private static readonly Dictionary<string, bool> Fm4Techniques = MakeSet(new string[]
        {
            "Bentley_paint", "Bentley_wheel", "Default", "Delorean_Paint", "Stencil", "StencilRaw",
            "ZOnly", "badge", "badge2", "badge_chrome", "black", "black_glass",
            "black_glass_breakable", "blur_lip", "blur_lip_V2", "blur_rim", "blur_rim_V2", "body",
            "body_2", "body_3", "bottom", "bottom_2", "brakeShadowMap", "brakeShadowMapVariance",
            "brake_badge", "brake_brushed_metal", "brake_brushed_metal2", "brake_bump_metal", "brake_bump_metal2", "brake_bump_metal3",
            "brake_bump_metal4", "brake_carbon_fiber", "brake_carbon_fiber2", "brake_chrome", "brake_smooth_metal", "brushed_metal",
            "bump_metal", "bumper_frame", "cage", "caliper", "carbon_fiber", "carbon_fiber2",
            "carbon_fiber3", "carbon_fiber_aniso", "chrome", "chrome2", "chrome_2", "chrome_3",
            "chrome_badge", "chrome_blur_lip", "chrome_blur_lip_V2", "chrome_blur_rim", "chrome_blur_rim_V2", "chrome_bump",
            "chrome_rim", "chrome_rim_V2", "cloth", "cloth2", "cockpit_anodized_aluminum", "cockpit_badge",
            "cockpit_body", "cockpit_brushed_metal", "cockpit_brushed_metal2", "cockpit_bump_leather", "cockpit_bump_leather2", "cockpit_bump_leather3",
            "cockpit_bump_metal", "cockpit_bump_metal2", "cockpit_bump_metal3", "cockpit_bump_plastic", "cockpit_bump_plastic2", "cockpit_bump_plastic3",
            "cockpit_bump_plastic4", "cockpit_bump_plastic5", "cockpit_bump_stitching", "cockpit_bump_stitching2", "cockpit_bump_stitchingA", "cockpit_bump_stitchingB",
            "cockpit_bump_stitchingC", "cockpit_bump_stitchingD", "cockpit_bump_stitchingE", "cockpit_bump_stitchingF", "cockpit_bump_stitchingG", "cockpit_carbon_fiber",
            "cockpit_carbon_fiber2", "cockpit_carbon_fiber3", "cockpit_carbon_fiber_aniso", "cockpit_carbon_fiber_no_env", "cockpit_chrome", "cockpit_chrome2",
            "cockpit_chrome_bump", "cockpit_cloth", "cockpit_digital_gauge", "cockpit_dropShadow", "cockpit_embossed_badge", "cockpit_embossed_leather",
            "cockpit_embossed_leather2", "cockpit_embossed_metal", "cockpit_embossed_plastic", "cockpit_embossed_plastic2", "cockpit_gauge", "cockpit_gauge_emissive",
            "cockpit_gauge_emissive_unshadowed", "cockpit_gauge_needle", "cockpit_ghostInterior", "cockpit_glass", "cockpit_grille1", "cockpit_grille2",
            "cockpit_grille3", "cockpit_headliner", "cockpit_headliner2", "cockpit_leather", "cockpit_metal", "cockpit_metal2",
            "cockpit_mirrorMiddle", "cockpit_mottled", "cockpit_mottled2", "cockpit_mottled3", "cockpit_plastic", "cockpit_reflector",
            "cockpit_rubber", "cockpit_shadowMap", "cockpit_shadowMapVariance", "cockpit_smooth_metal", "cockpit_smooth_plastic", "cockpit_solid",
            "cockpit_stitching", "cockpit_suede", "cockpit_suede_headliner", "cockpit_tiling_cloth", "cockpit_tiling_cloth2", "cockpit_tiling_cloth3",
            "cockpit_unicolor_carbon_fiber", "cockpit_wood", "defrost_lines", "detail_glass_amber", "detail_glass_clear", "detail_glass_red",
            "dropShadow", "dropShadowBrake", "dropShadowRim", "dropShadowRim_V2", "dropShadowTire", "dropShadowTire_V2",
            "dullplastic", "emblem", "embossed_plastic", "embossed_plastic2", "engine", "engine_anodized_aluminum",
            "engine_badge", "engine_brushed_metal", "engine_brushed_metal2", "engine_bump_chrome", "engine_bump_metal", "engine_bump_metal2",
            "engine_bump_metal3", "engine_bump_metal4", "engine_bump_metal5", "engine_bump_plastic", "engine_bump_plastic2", "engine_bump_plastic3",
            "engine_bump_plastic4", "engine_bump_plastic5", "engine_carbon_fiber", "engine_carbon_fiber2", "engine_carbon_fiber3", "engine_chrome",
            "engine_cloth", "engine_embossed_CF", "engine_embossed_metal", "engine_embossed_plastic", "engine_embossed_plastic2", "engine_grille1",
            "engine_grille2", "engine_grille3", "engine_rubber", "engine_smooth_metal", "engine_smooth_plastic", "engine_solid",
            "exterior_tread", "frame", "frontwindow", "frontwindow_2", "gauge_emissive", "gauge_emissive_unshadowed",
            "gauge_needle", "gauge_red", "geoShadow", "geoShadowBrake", "geoShadowRim", "geoShadowRim_V2",
            "geoShadowTire", "geoShadowTire_V2", "ghost", "ghostBrake", "ghostInterior", "ghostRim",
            "ghostRim_V2", "ghostTire", "ghostTire_V2", "glass", "glass_2", "grille1",
            "grille1_alpha", "grille2", "grille2_alpha", "grille3", "head_light", "head_light2",
            "headlight_paint", "hub", "inner_rim", "inner_rim_V2", "inside_window", "interior",
            "interior_banner", "lights_amber", "lights_body", "lights_brushed_metal", "lights_chrome", "lights_glass",
            "lights_metal", "lights_plastic", "lights_red", "liveryBadge", "matte", "matte_colors",
            "matte_colors2", "metal", "metal2", "mirrorLeft", "mirrorMiddle", "mirrorRight",
            "misc", "missing", "missingBrake", "missingBrakePink", "missingPink", "missingTire",
            "missingTirePink", "missingTire_V2", "missingTire_V2Pink", "mottled", "outer_rim", "plastic",
            "plastic2", "race_kit_red", "red_glass", "reflector", "reventon_paint", "reverse_light",
            "reverse_light_lod0", "rim", "rimShadowMap", "rimShadowMapVariance", "rimShadowMapVariance_V2", "rimShadowMap_V2",
            "rim_V2", "rim_brushed_metal", "rim_brushed_metal2", "rim_bump_metal", "rim_bump_metal2", "rim_bump_metal3",
            "rim_bump_metal4", "rim_carbon_fiber", "rim_carbon_fiber2", "rim_carbon_fiber3", "rim_chrome", "rim_wheel_emblem",
            "rotor", "rubber", "rubber_trim", "rubber_trim_2", "sagaris_paint", "scaling_text",
            "shadowMap", "shadowMapVariance", "sidewall", "smooth_metal", "smooth_plastic", "stripe_badge",
            "tail_light", "tail_light2", "tail_light_lod0", "tail_light_on", "textured_reflector", "tiling_cloth2",
            "tire", "tireShadowMap", "tireShadowMapVariance", "tireShadowMapVariance_V2", "tireShadowMap_V2", "tire_V2",
            "tread", "undercarriage", "weatherstrip", "wheel_black", "wheel_emblem", "wheel_emblem_V2",
            "white_brake", "window", "window_2",
        });

    }

}
