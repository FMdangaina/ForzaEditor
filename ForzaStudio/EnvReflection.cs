using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ForzaStudio
{
    // Fake environment reflection for the viewport: the embedded sky picture (Resources\EnvMap.png) is read as a
    // sphere map and mixed over the lit mesh (alpha blend: the car keeps its brightness), weighted by a Fresnel term and by the material class (ShaderLook.Reflectivity).
    // BasicEffect (XNA 3.1) has no environment map, so this is a second blended pass with its own small effect.
    public class EnvReflection : IDisposable
    {
        private const string Source =
            "float4x4 WorldView; float4x4 WorldViewProj; float Strength; texture Env;\n" +
            "sampler EnvSampler = sampler_state { Texture = <Env>; MinFilter = Linear; MagFilter = Linear; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };\n" +
            "struct VsOut { float4 Pos : POSITION; float3 N : TEXCOORD0; float3 P : TEXCOORD1; };\n" +
            "VsOut VS(float4 pos : POSITION, float3 nrm : NORMAL)\n" +
            "{ VsOut o; o.Pos = mul(pos, WorldViewProj); o.N = mul(nrm, (float3x3)WorldView); o.P = mul(pos, WorldView).xyz; return o; }\n" +
            "float4 PS(VsOut i) : COLOR0\n" +
            "{ float3 n = normalize(i.N); float3 v = normalize(i.P); float3 r = reflect(v, n);\n" +
            "  float f = 1 - saturate(dot(n, -v)); f = f * f * f; f = 0.2 + 0.8 * f;\n" +
            "  float m = 2 * sqrt(r.x * r.x + r.y * r.y + (r.z + 1) * (r.z + 1));\n" +
            "  float2 uv = float2(r.x / m + 0.5, 0.5 - r.y / m);\n" +
            "  return float4(tex2D(EnvSampler, uv).rgb, saturate(Strength * f)); }\n" +
            "technique T { pass P0 { VertexShader = compile vs_2_0 VS(); PixelShader = compile ps_2_0 PS(); } }\n";

        private Effect effect;
        private Texture2D texture;

        public bool Ready { get { return this.effect != null && this.texture != null; } }

        // Returns an unusable (Ready = false) instance when the effect compiler or the picture is not available.
        public EnvReflection(GraphicsDevice device)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EnvMap.png"))
                {
                    if (stream == null) return;
                    this.texture = Texture2D.FromFile(device, stream);
                }
                CompiledEffect compiled = Effect.CompileEffectFromSource(Source, null, null, CompilerOptions.None, TargetPlatform.Windows);
                if (!compiled.Success) return;
                this.effect = new Effect(device, compiled.GetEffectCode(), CompilerOptions.None, null);
                this.effect.Parameters["Env"].SetValue(this.texture);
            }
            catch (Exception)
            {
                this.effect = null;
            }
        }

        // Draws `mesh` again with the reflection added; the caller restores its own render state.
        public void Draw(GraphicsDevice device, ForzaMesh mesh, Matrix world, Matrix view, Matrix projection, float strength)
        {
            Matrix worldView = world * view;
            this.effect.Parameters["WorldView"].SetValue(worldView);
            this.effect.Parameters["WorldViewProj"].SetValue(worldView * projection);
            this.effect.Parameters["Strength"].SetValue(strength);
            device.RenderState.AlphaBlendEnable = true;
            device.RenderState.SourceBlend = Blend.SourceAlpha;
            device.RenderState.DestinationBlend = Blend.InverseSourceAlpha;
            device.RenderState.DepthBufferWriteEnable = false;
            this.effect.Begin();
            foreach (EffectPass pass in this.effect.CurrentTechnique.Passes)
            {
                pass.Begin();
                mesh.Draw(ref device);
                pass.End();
            }
            this.effect.End();
        }

        public void Dispose()
        {
            if (this.effect != null) { this.effect.Dispose(); this.effect = null; }
            if (this.texture != null) { this.texture.Dispose(); this.texture = null; }
        }
    }
}
