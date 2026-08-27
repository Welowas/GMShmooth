using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Models;

namespace GMShmoothCli
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 3)
            {
                Console.Error.Write("Invalid args");
                Console.Error.Write("Usage: converterGMS.exe input output [worldObjectName]");
                Environment.Exit(1);
            }
            string input = args[0];
            string output = args[1];
            string worldObjectName = "";
            if (args.Length == 3)
            {
                worldObjectName = args[2];
            }

            UndertaleData data = UndertaleIO.Read(new FileStream(input, FileMode.Open, FileAccess.Read));
            if (data.Shaders.ByName(@"__SMOOTH_sh_pxUpscale") != null)
            {
                Console.Error.Write("This game has already been injected with Plasma's Smoothing Mode");
                Environment.Exit(1);
            }
            /// Shaders
            UndertaleShader shader = new()
            {
                Name = data.Strings.MakeString(@"__SMOOTH_sh_pxUpscale")
            };
            if (!data.IsGameMaker2())
            {
                shader.Type = UndertaleShader.ShaderType.HLSL9;
                shader.HLSL9_Vertex = data.Strings.MakeString("""
                    #define	MATRIX_VIEW 					0
                    #define	MATRIX_PROJECTION 				1
                    #define	MATRIX_WORLD 					2
                    #define	MATRIX_WORLD_VIEW 				3
                    #define	MATRIX_WORLD_VIEW_PROJECTION 	4
                    #define	MATRICES_MAX					5

                    float4x4 	gm_Matrices[MATRICES_MAX] : register(c0);

                    bool 	gm_LightingEnabled;
                    bool 	gm_VS_FogEnabled;
                    float 	gm_FogStart;
                    float 	gm_RcpFogRange;

                    #define	MAX_VS_LIGHTS					8
                    float4 gm_AmbientColour;							// rgb=colour, a=1
                    float3 gm_Lights_Direction[MAX_VS_LIGHTS];			// normalised direction
                    float4 gm_Lights_PosRange[MAX_VS_LIGHTS];			// X,Y,Z position,  W range
                    float4 gm_Lights_Colour[MAX_VS_LIGHTS];				// rgb=colour, a=1

                    struct VS_INPUT
                    {
                        float3 in_Position : POSITION0;
                        float2 in_TextureCoord : TEXCOORD0;
                    };

                    struct VS_OUTPUT
                    {
                        float2 vTexcoord : TEXCOORD0;
                        float4 pos : SV_POSITION;
                    };

                    VS_OUTPUT main(VS_INPUT input)
                    {
                        VS_OUTPUT output;
                        float4 object_space_pos = float4(input.in_Position.x, input.in_Position.y, input.in_Position.z, 1.0);
                        output.vTexcoord = float2(input.in_TextureCoord.x * 800.0, input.in_TextureCoord.y * 608.0);
                        output.pos = mul(gm_Matrices[MATRIX_WORLD_VIEW_PROJECTION], object_space_pos);
                        return output;
                    }

                    """);
                shader.HLSL9_Fragment = data.Strings.MakeString("""
                    // GameMaker reserved and common types/inputs

                    sampler2D gm_BaseTexture : register(S0);

                    bool 	gm_PS_FogEnabled;
                    float4 	gm_FogColour;
                    bool 	gm_AlphaTestEnabled;
                    float4	gm_AlphaRefValue;
                    struct PS_INPUT
                    {
                        float2 vTexcoord : TEXCOORD0;
                    };

                    float2 u_texelsPerPixel;

                    float4 main(PS_INPUT input) : SV_Target
                    {
                        float2 locationInTexel = frac(input.vTexcoord);
                        float2 interp_amount = clamp(locationInTexel / u_texelsPerPixel, 0.0, 0.5) +
                            clamp((locationInTexel - float2(1.0, 1.0)) / u_texelsPerPixel + float2(0.5, 0.5), 0.0, 0.5);
                        float2 finalCoords = (floor(input.vTexcoord) + interp_amount) / float2(800.0, 608.0);
                        return tex2D(gm_BaseTexture, finalCoords);
                    }

                    """);
            }
            else
            {
                shader.Type = UndertaleShader.ShaderType.HLSL11;
                shader.HLSL11_VertexData.Data = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 188, 4, 0, 0, 52, 0, 0, 0, 72, 0, 0, 0, 104, 0, 0, 0, 104, 0, 0, 0, 104, 0, 0, 0, 164, 0, 0, 0, 96, 5, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 64, 1, 0, 0, 117, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 64, 1, 0, 0, 5, 0, 0, 0, 4, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 129, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 7, 0, 0, 0, 138, 5, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 3, 0, 0, 0, 147, 5, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 5, 0, 0, 0, 15, 0, 0, 0, 68, 88, 66, 67, 30, 66, 169, 240, 78, 123, 217, 37, 157, 75, 35, 56, 187, 209, 160, 254, 1, 0, 0, 0, 188, 4, 0, 0, 6, 0, 0, 0, 56, 0, 0, 0, 60, 1, 0, 0, 128, 2, 0, 0, 252, 2, 0, 0, 216, 3, 0, 0, 72, 4, 0, 0, 65, 111, 110, 57, 252, 0, 0, 0, 252, 0, 0, 0, 0, 2, 254, 255, 200, 0, 0, 0, 52, 0, 0, 0, 1, 0, 36, 0, 0, 0, 48, 0, 0, 0, 48, 0, 0, 0, 36, 0, 1, 0, 48, 0, 0, 0, 16, 0, 4, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 254, 255, 81, 0, 0, 5, 5, 0, 15, 160, 0, 0, 72, 68, 0, 0, 24, 68, 0, 0, 0, 0, 0, 0, 0, 0, 31, 0, 0, 2, 5, 0, 0, 128, 0, 0, 15, 144, 31, 0, 0, 2, 5, 0, 1, 128, 1, 0, 15, 144, 31, 0, 0, 2, 5, 0, 2, 128, 2, 0, 15, 144, 5, 0, 0, 3, 0, 0, 3, 224, 1, 0, 228, 144, 5, 0, 228, 160, 5, 0, 0, 3, 0, 0, 15, 128, 0, 0, 85, 144, 2, 0, 228, 160, 4, 0, 0, 4, 0, 0, 15, 128, 1, 0, 228, 160, 0, 0, 0, 144, 0, 0, 228, 128, 4, 0, 0, 4, 0, 0, 15, 128, 3, 0, 228, 160, 0, 0, 170, 144, 0, 0, 228, 128, 2, 0, 0, 3, 0, 0, 15, 128, 0, 0, 228, 128, 4, 0, 228, 160, 4, 0, 0, 4, 0, 0, 3, 192, 0, 0, 255, 128, 0, 0, 228, 160, 0, 0, 228, 128, 1, 0, 0, 2, 0, 0, 12, 192, 0, 0, 228, 128, 1, 0, 0, 2, 1, 0, 15, 224, 2, 0, 228, 144, 255, 255, 0, 0, 83, 72, 68, 82, 60, 1, 0, 0, 64, 0, 1, 0, 79, 0, 0, 0, 89, 0, 0, 4, 70, 142, 32, 0, 0, 0, 0, 0, 20, 0, 0, 0, 95, 0, 0, 3, 114, 16, 16, 0, 0, 0, 0, 0, 95, 0, 0, 3, 50, 16, 16, 0, 1, 0, 0, 0, 95, 0, 0, 3, 242, 16, 16, 0, 2, 0, 0, 0, 103, 0, 0, 4, 242, 32, 16, 0, 0, 0, 0, 0, 1, 0, 0, 0, 101, 0, 0, 3, 50, 32, 16, 0, 1, 0, 0, 0, 101, 0, 0, 3, 242, 32, 16, 0, 2, 0, 0, 0, 104, 0, 0, 2, 1, 0, 0, 0, 56, 0, 0, 8, 242, 0, 16, 0, 0, 0, 0, 0, 86, 21, 16, 0, 0, 0, 0, 0, 70, 142, 32, 0, 0, 0, 0, 0, 17, 0, 0, 0, 50, 0, 0, 10, 242, 0, 16, 0, 0, 0, 0, 0, 70, 142, 32, 0, 0, 0, 0, 0, 16, 0, 0, 0, 6, 16, 16, 0, 0, 0, 0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 50, 0, 0, 10, 242, 0, 16, 0, 0, 0, 0, 0, 70, 142, 32, 0, 0, 0, 0, 0, 18, 0, 0, 0, 166, 26, 16, 0, 0, 0, 0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 0, 0, 0, 8, 242, 32, 16, 0, 0, 0, 0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 70, 142, 32, 0, 0, 0, 0, 0, 19, 0, 0, 0, 56, 0, 0, 10, 50, 32, 16, 0, 1, 0, 0, 0, 70, 16, 16, 0, 1, 0, 0, 0, 2, 64, 0, 0, 0, 0, 72, 68, 0, 0, 24, 68, 0, 0, 0, 0, 0, 0, 0, 0, 54, 0, 0, 5, 242, 32, 16, 0, 2, 0, 0, 0, 70, 30, 16, 0, 2, 0, 0, 0, 62, 0, 0, 1, 83, 84, 65, 84, 116, 0, 0, 0, 7, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 82, 68, 69, 70, 212, 0, 0, 0, 1, 0, 0, 0, 84, 0, 0, 0, 1, 0, 0, 0, 28, 0, 0, 0, 0, 4, 254, 255, 0, 129, 0, 0, 160, 0, 0, 0, 60, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 103, 109, 95, 86, 83, 84, 114, 97, 110, 115, 102, 111, 114, 109, 66, 117, 102, 102, 101, 114, 0, 171, 171, 171, 60, 0, 0, 0, 1, 0, 0, 0, 108, 0, 0, 0, 64, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 132, 0, 0, 0, 0, 0, 0, 0, 64, 1, 0, 0, 2, 0, 0, 0, 144, 0, 0, 0, 0, 0, 0, 0, 103, 109, 95, 77, 97, 116, 114, 105, 99, 101, 115, 0, 3, 0, 3, 0, 4, 0, 4, 0, 5, 0, 0, 0, 0, 0, 0, 0, 77, 105, 99, 114, 111, 115, 111, 102, 116, 32, 40, 82, 41, 32, 72, 76, 83, 76, 32, 83, 104, 97, 100, 101, 114, 32, 67, 111, 109, 112, 105, 108, 101, 114, 32, 57, 46, 51, 48, 46, 57, 50, 48, 48, 46, 49, 54, 51, 56, 52, 0, 171, 73, 83, 71, 78, 104, 0, 0, 0, 3, 0, 0, 0, 8, 0, 0, 0, 80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 7, 7, 0, 0, 89, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, 3, 3, 0, 0, 98, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 2, 0, 0, 0, 15, 15, 0, 0, 80, 79, 83, 73, 84, 73, 79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0, 67, 79, 76, 79, 82, 0, 79, 83, 71, 78, 108, 0, 0, 0, 3, 0, 0, 0, 8, 0, 0, 0, 80, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 15, 0, 0, 0, 92, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, 3, 12, 0, 0, 101, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 2, 0, 0, 0, 15, 0, 0, 0, 83, 86, 95, 80, 79, 83, 73, 84, 73, 79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0, 67, 79, 76, 79, 82, 0, 171, 103, 109, 95, 86, 83, 84, 114, 97, 110, 115, 102, 111, 114, 109, 66, 117, 102, 102, 101, 114, 0, 103, 109, 95, 77, 97, 116, 114, 105, 99, 101, 115, 0, 80, 79, 83, 73, 84, 73, 79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0, 67, 79, 76, 79, 82, 0, 0, 0, 0, 0, 0, 0, 0];
                shader.HLSL11_VertexData.IsNull = false;
                shader.HLSL11_PixelData.Data = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 228, 5, 0, 0, 52, 0, 0, 0, 72, 0, 0, 0, 104, 0, 0, 0, 116, 0, 0, 0, 128, 0, 0, 0, 188, 0, 0, 0, 196, 6, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 16, 0, 0, 0, 205, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 160, 6, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 175, 6, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 222, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 15, 0, 0, 0, 234, 6, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 3, 0, 0, 0, 243, 6, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 5, 0, 0, 0, 15, 0, 0, 0, 68, 88, 66, 67, 113, 54, 251, 217, 7, 142, 88, 217, 38, 177, 220, 34, 19, 63, 232, 42, 1, 0, 0, 0, 228, 5, 0, 0, 6, 0, 0, 0, 56, 0, 0, 0, 184, 1, 0, 0, 132, 3, 0, 0, 0, 4, 0, 0, 60, 5, 0, 0, 176, 5, 0, 0, 65, 111, 110, 57, 120, 1, 0, 0, 120, 1, 0, 0, 0, 2, 255, 255, 68, 1, 0, 0, 52, 0, 0, 0, 1, 0, 40, 0, 0, 0, 52, 0, 0, 0, 52, 0, 1, 0, 36, 0, 0, 0, 52, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2, 255, 255, 81, 0, 0, 5, 1, 0, 15, 160, 0, 0, 0, 0, 0, 0, 0, 63, 0, 0, 128, 191, 0, 0, 0, 0, 81, 0, 0, 5, 2, 0, 15, 160, 10, 215, 163, 58, 54, 148, 215, 58, 0, 0, 0, 0, 0, 0, 0, 0, 31, 0, 0, 2, 0, 0, 0, 128, 0, 0, 3, 176, 31, 0, 0, 2, 0, 0, 0, 144, 0, 8, 15, 160, 6, 0, 0, 2, 0, 0, 1, 128, 0, 0, 0, 160, 6, 0, 0, 2, 0, 0, 2, 128, 0, 0, 85, 160, 19, 0, 0, 2, 0, 0, 12, 128, 0, 0, 27, 176, 2, 0, 0, 3, 1, 0, 3, 128, 0, 0, 27, 128, 1, 0, 170, 160, 4, 0, 0, 4, 1, 0, 3, 128, 1, 0, 228, 128, 0, 0, 228, 128, 1, 0, 85, 160, 5, 0, 0, 3, 0, 0, 3, 128, 0, 0, 228, 128, 0, 0, 27, 128, 2, 0, 0, 3, 0, 0, 12, 128, 0, 0, 228, 129, 0, 0, 27, 176, 11, 0, 0, 3, 1, 0, 12, 128, 0, 0, 27, 128, 1, 0, 0, 160, 10, 0, 0, 3, 0, 0, 3, 128, 1, 0, 27, 128, 1, 0, 85, 160, 11, 0, 0, 3, 2, 0, 3, 128, 1, 0, 228, 128, 1, 0, 0, 160, 10, 0, 0, 3, 1, 0, 3, 128, 2, 0, 228, 128, 1, 0, 85, 160, 2, 0, 0, 3, 0, 0, 3, 128, 0, 0, 228, 128, 1, 0, 228, 128, 2, 0, 0, 3, 0, 0, 3, 128, 0, 0, 228, 128, 0, 0, 27, 128, 5, 0, 0, 3, 0, 0, 3, 128, 0, 0, 228, 128, 2, 0, 228, 160, 66, 0, 0, 3, 0, 0, 15, 128, 0, 0, 228, 128, 0, 8, 228, 160, 1, 0, 0, 2, 0, 8, 15, 128, 0, 0, 228, 128, 255, 255, 0, 0, 83, 72, 68, 82, 196, 1, 0, 0, 64, 0, 0, 0, 113, 0, 0, 0, 89, 0, 0, 4, 70, 142, 32, 0, 0, 0, 0, 0, 1, 0, 0, 0, 90, 0, 0, 3, 0, 96, 16, 0, 0, 0, 0, 0, 88, 24, 0, 4, 0, 112, 16, 0, 0, 0, 0, 0, 85, 85, 0, 0, 98, 16, 0, 3, 50, 16, 16, 0, 1, 0, 0, 0, 101, 0, 0, 3, 242, 32, 16, 0, 0, 0, 0, 0, 104, 0, 0, 2, 1, 0, 0, 0, 26, 0, 0, 5, 50, 0, 16, 0, 0, 0, 0, 0, 70, 16, 16, 0, 1, 0, 0, 0, 0, 0, 0, 10, 194, 0, 16, 0, 0, 0, 0, 0, 6, 4, 16, 0, 0, 0, 0, 0, 2, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 191, 0, 0, 128, 191, 14, 0, 0, 8, 242, 0, 16, 0, 0, 0, 0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 70, 132, 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 194, 0, 16, 0, 0, 0, 0, 0, 166, 14, 16, 0, 0, 0, 0, 0, 2, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 63, 0, 0, 0, 63, 52, 0, 0, 10, 242, 0, 16, 0, 0, 0, 0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 2, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 51, 0, 0, 10, 242, 0, 16, 0, 0, 0, 0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 2, 64, 0, 0, 0, 0, 0, 63, 0, 0, 0, 63, 0, 0, 0, 63, 0, 0, 0, 63, 0, 0, 0, 7, 50, 0, 16, 0, 0, 0, 0, 0, 230, 10, 16, 0, 0, 0, 0, 0, 70, 0, 16, 0, 0, 0, 0, 0, 65, 0, 0, 5, 194, 0, 16, 0, 0, 0, 0, 0, 6, 20, 16, 0, 1, 0, 0, 0, 0, 0, 0, 7, 50, 0, 16, 0, 0, 0, 0, 0, 70, 0, 16, 0, 0, 0, 0, 0, 230, 10, 16, 0, 0, 0, 0, 0, 56, 0, 0, 10, 50, 0, 16, 0, 0, 0, 0, 0, 70, 0, 16, 0, 0, 0, 0, 0, 2, 64, 0, 0, 10, 215, 163, 58, 54, 148, 215, 58, 0, 0, 0, 0, 0, 0, 0, 0, 69, 0, 0, 9, 242, 32, 16, 0, 0, 0, 0, 0, 70, 0, 16, 0, 0, 0, 0, 0, 70, 126, 16, 0, 0, 0, 0, 0, 0, 96, 16, 0, 0, 0, 0, 0, 62, 0, 0, 1, 83, 84, 65, 84, 116, 0, 0, 0, 12, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 82, 68, 69, 70, 52, 1, 0, 0, 1, 0, 0, 0, 172, 0, 0, 0, 3, 0, 0, 0, 28, 0, 0, 0, 0, 4, 255, 255, 0, 129, 0, 0, 0, 1, 0, 0, 124, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 139, 0, 0, 0, 2, 0, 0, 0, 5, 0, 0, 0, 4, 0, 0, 0, 255, 255, 255, 255, 0, 0, 0, 0, 1, 0, 0, 0, 13, 0, 0, 0, 160, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 103, 109, 95, 66, 97, 115, 101, 84, 101, 120, 116, 117, 114, 101, 0, 103, 109, 95, 66, 97, 115, 101, 84, 101, 120, 116, 117, 114, 101, 79, 98, 106, 101, 99, 116, 0, 36, 71, 108, 111, 98, 97, 108, 115, 0, 171, 171, 171, 160, 0, 0, 0, 1, 0, 0, 0, 196, 0, 0, 0, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 220, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 2, 0, 0, 0, 240, 0, 0, 0, 0, 0, 0, 0, 117, 95, 116, 101, 120, 101, 108, 115, 80, 101, 114, 80, 105, 120, 101, 108, 0, 171, 171, 171, 1, 0, 3, 0, 1, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 77, 105, 99, 114, 111, 115, 111, 102, 116, 32, 40, 82, 41, 32, 72, 76, 83, 76, 32, 83, 104, 97, 100, 101, 114, 32, 67, 111, 109, 112, 105, 108, 101, 114, 32, 57, 46, 51, 48, 46, 57, 50, 48, 48, 46, 49, 54, 51, 56, 52, 0, 171, 73, 83, 71, 78, 108, 0, 0, 0, 3, 0, 0, 0, 8, 0, 0, 0, 80, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 15, 0, 0, 0, 92, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, 3, 3, 0, 0, 101, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 2, 0, 0, 0, 15, 0, 0, 0, 83, 86, 95, 80, 79, 83, 73, 84, 73, 79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0, 67, 79, 76, 79, 82, 0, 171, 79, 83, 71, 78, 44, 0, 0, 0, 1, 0, 0, 0, 8, 0, 0, 0, 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 15, 0, 0, 0, 83, 86, 95, 84, 65, 82, 71, 69, 84, 0, 171, 171, 103, 109, 95, 66, 97, 115, 101, 84, 101, 120, 116, 117, 114, 101, 0, 103, 109, 95, 66, 97, 115, 101, 84, 101, 120, 116, 117, 114, 101, 79, 98, 106, 101, 99, 116, 0, 36, 71, 108, 111, 98, 97, 108, 115, 0, 117, 95, 116, 101, 120, 101, 108, 115, 80, 101, 114, 80, 105, 120, 101, 108, 0, 83, 86, 95, 80, 79, 83, 73, 84, 73, 79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0, 67, 79, 76, 79, 82, 0, 0, 0, 0, 0, 0, 0, 0];
                shader.HLSL11_PixelData.IsNull = false;
            }
            data.Shaders.Add(shader);

            /// Objects
            List<string> potentialWorldStrings =
            [
                worldObjectName,
                "objWorld",
                "objworld",
                "obj_World",
                "obj_world",
                "o_World",
                "o_world",
                "oWorld",
                "world",
                "World",
                "oworld"
            ];
            UndertaleGameObject? world = null;
            foreach (string worldString in potentialWorldStrings)
            {
                if (worldString == "")
                {
                    continue;
                }
                world = data.GameObjects.ByName(worldString);
                if (world != null)
                {
                    break;
                }
            }
            if (world == null)
            {
                Console.Error.Write("Unable to find the world object");
                Environment.Exit(1);
            }

            CodeImportGroup cig = new(data);
            string defaultSmoothRemovalCode;
            if (!data.IsGameMaker2())
            {
                defaultSmoothRemovalCode = """
                    /*"/*'/**/
                    /// PLASMA_SMOOTH
                    texture_set_interpolation(false);
                    """;

                cig.QueueAppend(world.EventHandlerFor(EventType.Draw, EventSubtypeDraw.PostDraw, data), """
                    /*"/*'/**/
                    /// PLASMA_SMOOTH
                    var __SMOOTH_windowWidth = window_get_width();
                    var __SMOOTH_windowHeight = window_get_height();

                    var __SMOOTH_aspectRatio = __SMOOTH_windowWidth / __SMOOTH_windowHeight;
                    var __SMOOTH_aspectRatioRatio = __SMOOTH_aspectRatio / (800/608);

                    var __SMOOTH_pixelScaling = (__SMOOTH_aspectRatioRatio < 1 && __SMOOTH_windowWidth mod 800 != 0) || (__SMOOTH_aspectRatioRatio > 1 && __SMOOTH_windowHeight mod 608 != 0) || (__SMOOTH_windowWidth mod 800 != 0 && __SMOOTH_windowHeight mod 608 != 0);

                    texture_set_repeat(false);
                    draw_enable_alphablend(false);

                    if(__SMOOTH_pixelScaling){
                        texture_set_interpolation(true);
                        shader_set(__SMOOTH_sh_pxUpscale);
                    }  

                    if(__SMOOTH_aspectRatioRatio < 1){
                        var __SMOOTH_canvasHeight = __SMOOTH_windowWidth*608/800;
                        var __SMOOTH_vertOutPixels = (__SMOOTH_windowHeight - __SMOOTH_canvasHeight) / 2;
                        shader_set_uniform_f(__SMOOTH_u_texelsPerPixel, 800./__SMOOTH_windowWidth, 608./__SMOOTH_canvasHeight);
                        draw_surface_stretched(application_surface, 0, __SMOOTH_vertOutPixels, __SMOOTH_windowWidth, __SMOOTH_canvasHeight);
                    }
                    else{
                        var __SMOOTH_canvasWidth = __SMOOTH_windowHeight*800/608;
                        var __SMOOTH_horOutPixels = (__SMOOTH_windowWidth - __SMOOTH_canvasWidth) / 2;
                        shader_set_uniform_f(__SMOOTH_u_texelsPerPixel, 800./__SMOOTH_canvasWidth, 608./__SMOOTH_windowHeight);
                        draw_surface_stretched(application_surface, __SMOOTH_horOutPixels, 0, __SMOOTH_canvasWidth, __SMOOTH_windowHeight);
                    }

                    if(__SMOOTH_pixelScaling){
                        shader_reset();
                        texture_set_interpolation(false);
                    }

                    draw_enable_alphablend(true);
                    """);
            }
            else
            {
                defaultSmoothRemovalCode = """
                    /*"/*'/**/
                    /// PLASMA_SMOOTH
                    gpu_set_texfilter(false);
                    """;

                cig.QueueAppend(world.EventHandlerFor(EventType.Draw, EventSubtypeDraw.PostDraw, data), """
                    /*"/*'/**/
                    var windowWidth = window_get_width();
                    var windowHeight = window_get_height();

                    var aspectRatio = windowWidth / windowHeight;
                    var aspectRatioRatio = aspectRatio / (800/608);

                    var pixelScaling = (aspectRatioRatio < 1 && windowWidth mod 800 != 0) || (aspectRatioRatio > 1 && windowHeight mod 608 != 0) || (windowWidth mod 800 != 0 && windowHeight mod 608 != 0);

                    gpu_set_texrepeat(false);
                    gpu_set_blendenable(false);

                    if(pixelScaling){
                        gpu_set_texfilter(true);
                        shader_set(__SMOOTH_sh_pxUpscale);
                    }  

                    if(aspectRatioRatio < 1){
                        var canvasHeight = windowWidth*608/800;
                        var vertOutPixels = (windowHeight - canvasHeight) / 2;
                        shader_set_uniform_f(__SMOOTH_u_texelsPerPixel, 800./windowWidth, 608./canvasHeight);
                        draw_surface_stretched(application_surface, 0, vertOutPixels, windowWidth, canvasHeight);
                    }
                    else{
                        var canvasWidth = windowHeight*800/608;
                        var horOutPixels = (windowWidth - canvasWidth) / 2;
                        shader_set_uniform_f(__SMOOTH_u_texelsPerPixel, 800./canvasWidth, 608./windowHeight);
                        draw_surface_stretched(application_surface, horOutPixels, 0, canvasWidth, windowHeight);
                    }

                    if(pixelScaling){
                        shader_reset();
                        gpu_set_texfilter(false);
                    }

                    gpu_set_blendenable(true);


                    /*
                    /// PLASMA_SMOOTH
                    var __SMOOTH_windowWidth = window_get_width();
                    var __SMOOTH_windowHeight = window_get_height();
                    var __SMOOTH_guiWidth = display_get_gui_width();
                    var __SMOOTH_guiHeight = display_get_gui_height();

                    var __SMOOTH_aspectRatio = __SMOOTH_windowWidth / __SMOOTH_windowHeight;
                    var __SMOOTH_aspectRatioRatio = __SMOOTH_aspectRatio / (800/608);

                    var __SMOOTH_pixelScaling = (__SMOOTH_aspectRatioRatio < 1 && __SMOOTH_windowWidth mod 800 != 0) || (__SMOOTH_aspectRatioRatio > 1 && __SMOOTH_windowHeight mod 608 != 0) || (__SMOOTH_windowWidth mod 800 != 0 && __SMOOTH_windowHeight mod 608 != 0);

                    gpu_set_texrepeat(false);
                    gpu_set_blendenable(false);

                    if(__SMOOTH_pixelScaling){
                        gpu_set_texfilter(true);
                        shader_set(__SMOOTH_sh_pxUpscale);
                    }  

                    if(__SMOOTH_aspectRatioRatio < 1){
                        var __SMOOTH_canvasHeight = __SMOOTH_windowWidth*608/800;
                        var __SMOOTH_canvasHeightStretched = __SMOOTH_guiWidth*608/800;
                        var __SMOOTH_vertOutPixels = (__SMOOTH_guiHeight - __SMOOTH_canvasHeightStretched) / 2;
                        shader_set_uniform_f(__SMOOTH_u_texelsPerPixel, 800./__SMOOTH_windowWidth, 608./__SMOOTH_canvasHeight);
                        draw_surface_stretched(application_surface, 0, __SMOOTH_vertOutPixels, __SMOOTH_guiWidth, __SMOOTH_canvasHeightStretched);
                    }
                    else{
                        var __SMOOTH_canvasWidth = __SMOOTH_windowHeight*800/608;
                        var __SMOOTH_canvasWidthStretched = __SMOOTH_guiHeight*800/608;
                        var __SMOOTH_horOutPixels = (__SMOOTH_guiWidth - __SMOOTH_canvasWidthStretched) / 2;
                        shader_set_uniform_f(__SMOOTH_u_texelsPerPixel, 800./__SMOOTH_canvasWidth, 608./__SMOOTH_windowHeight);
                        draw_surface_stretched(application_surface, __SMOOTH_horOutPixels, 0, __SMOOTH_canvasWidthStretched, __SMOOTH_guiHeight);
                    }

                    if(__SMOOTH_pixelScaling){
                        shader_reset();
                        gpu_set_texfilter(false);
                    }

                    gpu_set_blendenable(true);
                    */

                    """);
            }
            cig.QueueAppend(world.EventHandlerFor(EventType.Create, data), """
                /*"/*'/**/
                /// PLASMA_SMOOTH
                __SMOOTH_u_texelsPerPixel = shader_get_uniform(__SMOOTH_sh_pxUpscale,"u_texelsPerPixel");
                """);
            cig.QueueAppend(world.EventHandlerFor(EventType.Draw, EventSubtypeDraw.PreDraw, data), @defaultSmoothRemovalCode);
            cig.QueueAppend(world.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawBegin, data), @defaultSmoothRemovalCode);
            cig.QueueAppend(world.EventHandlerFor(EventType.Draw, EventSubtypeDraw.Draw, data), @defaultSmoothRemovalCode);
            cig.QueueAppend(world.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawEnd, data), @defaultSmoothRemovalCode + """
                
                application_surface_draw_enable(false);
                """);
            cig.QueueAppend(world.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUIBegin, data), @defaultSmoothRemovalCode);
            cig.QueueAppend(world.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUI, data), @defaultSmoothRemovalCode);
            cig.QueueAppend(world.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUIEnd, data), @defaultSmoothRemovalCode);
            cig.Import();

            /// Recompile
            UndertaleIO.Write(new FileStream(output, FileMode.Create), data);
            Console.WriteLine("Success!");
        }
    }
}
