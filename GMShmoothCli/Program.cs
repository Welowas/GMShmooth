using System.Diagnostics;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Models;

namespace GMShmoothCli
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                if (args.Length != 1)
                {
                    Console.Error.WriteLine("Invalid arguments.");
                    Console.WriteLine("Usage: GMShmooth.exe <data.win or game.exe>");
                    Console.WriteLine();
                    Console.WriteLine("Drag and drop the data.win or game's .exe file onto this application");
                    Console.WriteLine("to inject Plasma's Shader Smoothing (\"Shmoothing\") into the game.");
                    Console.WriteLine();
                    Console.WriteLine("The Shmoothing effect will automatically activate whenever the game's resolution is at a size");
                    Console.WriteLine("where its pixels can't scale by a whole number.");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("Fetching data.win file...");
                Console.WriteLine();
                string gameFilePath = args[0];
                string gameFileExtension = Path.GetExtension(gameFilePath).ToLower();
                bool deleteOriginalGameFile = false;
                string dataWinFilePath;
                if (gameFileExtension == ".exe")
                {
                    string gameFolderPath = Path.GetDirectoryName(gameFilePath) ?? throw new InvalidOperationException("Game folder path is null.");
                    const string DataWinFileName = "data.win";
                    if (!File.Exists(dataWinFilePath = Path.Combine(gameFolderPath, DataWinFileName)))
                    {
                        Console.WriteLine("data.win file was not found in the game's folder.");
                        string extractionPath = Path.Combine(gameFolderPath, Path.GetFileNameWithoutExtension(gameFilePath));
                        Console.WriteLine("Attempting to extract it from the game's .exe file...");
                        using Process? sevenZip = Process.Start(new ProcessStartInfo
                        {
                            FileName = Path.Combine(AppContext.BaseDirectory, "7za.dll"),
                            ArgumentList = { "x", gameFilePath, "-o" + extractionPath, "-y" }
                        });
                        if (sevenZip is not null)
                        {
                            await sevenZip.WaitForExitAsync();
                            Console.WriteLine();
                            if (sevenZip.ExitCode == 0)
                            {
                                Console.WriteLine($"Successfully unzipped game into \"{extractionPath}\" folder.");
                                Console.WriteLine();
                            }
                            else
                            {
                                Console.Error.WriteLine($"Failed to extract data.win from the game's .exe file.");
                                Console.Error.WriteLine($"Exit code: {sevenZip.ExitCode}");
                                Console.ReadKey();
                                return;
                            }
                            deleteOriginalGameFile = true;
                        }

                        dataWinFilePath = Path.Combine(extractionPath, DataWinFileName);
                    }
                }
                else if (gameFileExtension == ".win")
                {
                    dataWinFilePath = gameFilePath;
                }
                else
                {
                    Console.Error.WriteLine("Invalid file format.");
                    Console.WriteLine("Please provide a valid data.win or game's .exe file made with GameMaker Studio 1 or 2.");
                    Console.ReadKey();
                    return;
                }

                if (!File.Exists(dataWinFilePath))
                {
                    Console.Error.WriteLine("data.win file was not found.");
                    Console.ReadKey();
                    return;
                }

                /// Decompile
                Console.WriteLine("data.win file has been located.");
                Console.WriteLine("Decompiling data.win file...");
                Console.WriteLine();
                FileStream readStream = new(dataWinFilePath, FileMode.Open, FileAccess.Read);
                UndertaleData gmsData = UndertaleIO.Read(readStream);
                readStream.Dispose();

                /// Shaders
                Console.WriteLine("Injecting Plasma's pixel upscaling shader...");
                Console.WriteLine();
                IList<UndertaleShader> gmsShaders = gmsData.Shaders;
                if (gmsShaders.ByName("__SHMOOTH_shPlasma") is not null)
                {
                    Console.Error.WriteLine("This game has already been applied with Shmoothing.");
                    Console.ReadKey();
                    return;
                }

                IList<UndertaleString> gmsStrings = gmsData.Strings;
                UndertaleShader gmsPlasmaShader = new()
                {
                    Name = gmsStrings.MakeString("__SHMOOTH_shPlasma")
                };
                if (!gmsData.IsGameMaker2())
                {
                    gmsPlasmaShader.Type = UndertaleShader.ShaderType.HLSL9;
                    gmsPlasmaShader.HLSL9_Vertex = gmsStrings.MakeString("""
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

                        uniform float2 uv_resolution;

                        VS_OUTPUT main(VS_INPUT input)
                        {
                            VS_OUTPUT output;
                            float4 object_space_pos = float4(input.in_Position.x, input.in_Position.y, input.in_Position.z, 1.0);
                            output.vTexcoord = float2(input.in_TextureCoord.x * uv_resolution.x, input.in_TextureCoord.y * uv_resolution.y);
                            output.pos = mul(gm_Matrices[MATRIX_WORLD_VIEW_PROJECTION], object_space_pos);
                            return output;
                        }

                        """);
                    gmsPlasmaShader.HLSL9_Fragment = gmsStrings.MakeString("""
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

                        uniform float2 u_texelsPerPixel;
                        uniform float2 uf_resolution;

                        float4 main(PS_INPUT input) : SV_Target
                        {
                            float2 locationInTexel = frac(input.vTexcoord);
                            float2 interp_amount = clamp(locationInTexel / u_texelsPerPixel, 0.0, 0.5) +
                                clamp((locationInTexel - float2(1.0, 1.0)) / u_texelsPerPixel + float2(0.5, 0.5), 0.0, 0.5);
                            float2 finalCoords = (floor(input.vTexcoord) + interp_amount) / uf_resolution;
                            return tex2D(gm_BaseTexture, finalCoords);
                        }

                        """);
                }
                else
                {
                    gmsPlasmaShader.Type = UndertaleShader.ShaderType.HLSL11;
                    gmsPlasmaShader.HLSL11_VertexData.Data =
                    [
                        1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 48, 5, 0, 0, 52, 0, 0, 0,
                        92, 0, 0, 0, 156, 0, 0, 0, 156, 0, 0, 0, 156, 0, 0, 0, 216, 0, 0, 0, 8, 6, 0, 0, 0, 0, 0, 0, 1,
                        0, 0, 0, 1, 0, 0, 0, 16, 0, 0, 0, 17, 6, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 64, 1, 0, 0,
                        38, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
                        52, 6, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 64, 1, 0, 0, 5, 0, 0, 0, 4, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0,
                        64, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 7, 0, 0, 0, 73, 6, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0,
                        5, 0, 0, 0, 3, 0, 0, 0, 82, 6, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 5, 0, 0, 0, 15, 0, 0, 0, 68, 88, 66,
                        67, 249, 86, 250, 143, 128, 135, 225, 229, 222, 37, 4, 7, 108, 85, 188, 162, 1, 0, 0, 0, 48, 5,
                        0, 0, 6, 0, 0, 0, 56, 0, 0, 0, 48, 1, 0, 0, 124, 2, 0, 0, 248, 2, 0, 0, 76, 4, 0, 0, 188, 4, 0,
                        0, 65, 111, 110, 57, 240, 0, 0, 0, 240, 0, 0, 0, 0, 2, 254, 255, 176, 0, 0, 0, 64, 0, 0, 0, 2, 0,
                        36, 0, 0, 0, 60, 0, 0, 0, 60, 0, 0, 0, 36, 0, 1, 0, 60, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 1,
                        0, 16, 0, 4, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 254, 255, 31, 0, 0, 2, 5, 0, 0, 128, 0, 0,
                        15, 144, 31, 0, 0, 2, 5, 0, 1, 128, 1, 0, 15, 144, 31, 0, 0, 2, 5, 0, 2, 128, 2, 0, 15, 144, 5,
                        0, 0, 3, 0, 0, 3, 224, 1, 0, 228, 144, 1, 0, 228, 160, 5, 0, 0, 3, 0, 0, 15, 128, 0, 0, 85, 144,
                        3, 0, 228, 160, 4, 0, 0, 4, 0, 0, 15, 128, 2, 0, 228, 160, 0, 0, 0, 144, 0, 0, 228, 128, 4, 0, 0,
                        4, 0, 0, 15, 128, 4, 0, 228, 160, 0, 0, 170, 144, 0, 0, 228, 128, 2, 0, 0, 3, 0, 0, 15, 128, 0,
                        0, 228, 128, 5, 0, 228, 160, 4, 0, 0, 4, 0, 0, 3, 192, 0, 0, 255, 128, 0, 0, 228, 160, 0, 0, 228,
                        128, 1, 0, 0, 2, 0, 0, 12, 192, 0, 0, 228, 128, 1, 0, 0, 2, 1, 0, 15, 224, 2, 0, 228, 144, 255,
                        255, 0, 0, 83, 72, 68, 82, 68, 1, 0, 0, 64, 0, 1, 0, 81, 0, 0, 0, 89, 0, 0, 4, 70, 142, 32, 0, 0,
                        0, 0, 0, 1, 0, 0, 0, 89, 0, 0, 4, 70, 142, 32, 0, 1, 0, 0, 0, 20, 0, 0, 0, 95, 0, 0, 3, 114, 16,
                        16, 0, 0, 0, 0, 0, 95, 0, 0, 3, 50, 16, 16, 0, 1, 0, 0, 0, 95, 0, 0, 3, 242, 16, 16, 0, 2, 0, 0,
                        0, 103, 0, 0, 4, 242, 32, 16, 0, 0, 0, 0, 0, 1, 0, 0, 0, 101, 0, 0, 3, 50, 32, 16, 0, 1, 0, 0, 0,
                        101, 0, 0, 3, 242, 32, 16, 0, 2, 0, 0, 0, 104, 0, 0, 2, 1, 0, 0, 0, 56, 0, 0, 8, 242, 0, 16, 0,
                        0, 0, 0, 0, 86, 21, 16, 0, 0, 0, 0, 0, 70, 142, 32, 0, 1, 0, 0, 0, 17, 0, 0, 0, 50, 0, 0, 10,
                        242, 0, 16, 0, 0, 0, 0, 0, 70, 142, 32, 0, 1, 0, 0, 0, 16, 0, 0, 0, 6, 16, 16, 0, 0, 0, 0, 0, 70,
                        14, 16, 0, 0, 0, 0, 0, 50, 0, 0, 10, 242, 0, 16, 0, 0, 0, 0, 0, 70, 142, 32, 0, 1, 0, 0, 0, 18,
                        0, 0, 0, 166, 26, 16, 0, 0, 0, 0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 0, 0, 0, 8, 242, 32, 16, 0, 0, 0,
                        0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 70, 142, 32, 0, 1, 0, 0, 0, 19, 0, 0, 0, 56, 0, 0, 8, 50, 32,
                        16, 0, 1, 0, 0, 0, 70, 16, 16, 0, 1, 0, 0, 0, 70, 128, 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 54, 0, 0,
                        5, 242, 32, 16, 0, 2, 0, 0, 0, 70, 30, 16, 0, 2, 0, 0, 0, 62, 0, 0, 1, 83, 84, 65, 84, 116, 0, 0,
                        0, 7, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 82, 68, 69, 70, 76, 1, 0, 0, 2, 0,
                        0, 0, 124, 0, 0, 0, 2, 0, 0, 0, 28, 0, 0, 0, 0, 4, 254, 255, 0, 129, 0, 0, 24, 1, 0, 0, 92, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 101, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 36, 71,
                        108, 111, 98, 97, 108, 115, 0, 103, 109, 95, 86, 83, 84, 114, 97, 110, 115, 102, 111, 114, 109,
                        66, 117, 102, 102, 101, 114, 0, 171, 171, 92, 0, 0, 0, 1, 0, 0, 0, 172, 0, 0, 0, 16, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 101, 0, 0, 0, 1, 0, 0, 0, 228, 0, 0, 0, 64, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        196, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 2, 0, 0, 0, 212, 0, 0, 0, 0, 0, 0, 0, 117, 118, 95, 114,
                        101, 115, 111, 108, 117, 116, 105, 111, 110, 0, 171, 171, 1, 0, 3, 0, 1, 0, 2, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 252, 0, 0, 0, 0, 0, 0, 0, 64, 1, 0, 0, 2, 0, 0, 0, 8, 1, 0, 0, 0, 0, 0, 0, 103, 109, 95,
                        77, 97, 116, 114, 105, 99, 101, 115, 0, 3, 0, 3, 0, 4, 0, 4, 0, 5, 0, 0, 0, 0, 0, 0, 0, 77, 105,
                        99, 114, 111, 115, 111, 102, 116, 32, 40, 82, 41, 32, 72, 76, 83, 76, 32, 83, 104, 97, 100, 101,
                        114, 32, 67, 111, 109, 112, 105, 108, 101, 114, 32, 57, 46, 51, 48, 46, 57, 50, 48, 48, 46, 49,
                        54, 51, 56, 52, 0, 171, 73, 83, 71, 78, 104, 0, 0, 0, 3, 0, 0, 0, 8, 0, 0, 0, 80, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 7, 7, 0, 0, 89, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0,
                        0, 0, 1, 0, 0, 0, 3, 3, 0, 0, 98, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 2, 0, 0, 0, 15,
                        15, 0, 0, 80, 79, 83, 73, 84, 73, 79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0, 67, 79, 76, 79,
                        82, 0, 79, 83, 71, 78, 108, 0, 0, 0, 3, 0, 0, 0, 8, 0, 0, 0, 80, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0,
                        3, 0, 0, 0, 0, 0, 0, 0, 15, 0, 0, 0, 92, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0,
                        3, 12, 0, 0, 101, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 2, 0, 0, 0, 15, 0, 0, 0, 83, 86,
                        95, 80, 79, 83, 73, 84, 73, 79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0, 67, 79, 76, 79, 82, 0,
                        171, 36, 71, 108, 111, 98, 97, 108, 115, 0, 103, 109, 95, 86, 83, 84, 114, 97, 110, 115, 102,
                        111, 114, 109, 66, 117, 102, 102, 101, 114, 0, 117, 118, 95, 114, 101, 115, 111, 108, 117, 116,
                        105, 111, 110, 0, 103, 109, 95, 77, 97, 116, 114, 105, 99, 101, 115, 0, 80, 79, 83, 73, 84, 73,
                        79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0, 67, 79, 76, 79, 82, 0
                    ];
                    gmsPlasmaShader.HLSL11_VertexData.IsNull = false;
                    gmsPlasmaShader.HLSL11_PixelData.Data =
                    [
                        1, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 4, 6, 0, 0, 52, 0, 0, 0,
                        72, 0, 0, 0, 136, 0, 0, 0, 148, 0, 0, 0, 160, 0, 0, 0, 220, 0, 0, 0, 4, 7, 0, 0, 0, 0, 0, 0, 1,
                        0, 0, 0, 2, 0, 0, 0, 16, 0, 0, 0, 13, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 5, 0, 0, 0, 2,
                        0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 30, 7, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 8, 0, 0, 0, 5, 0, 0, 0, 2,
                        0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 224, 6, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 239, 6, 0, 0, 0, 0, 0, 0,
                        1, 0, 0, 0, 44, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 15, 0, 0, 0, 56, 7, 0, 0, 0, 0, 0,
                        0, 1, 0, 0, 0, 5, 0, 0, 0, 3, 0, 0, 0, 65, 7, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 5, 0, 0, 0, 15, 0, 0,
                        0, 68, 88, 66, 67, 6, 27, 75, 97, 50, 172, 128, 214, 148, 122, 169, 245, 23, 157, 42, 23, 1, 0,
                        0, 0, 4, 6, 0, 0, 6, 0, 0, 0, 56, 0, 0, 0, 184, 1, 0, 0, 124, 3, 0, 0, 248, 3, 0, 0, 92, 5, 0, 0,
                        208, 5, 0, 0, 65, 111, 110, 57, 120, 1, 0, 0, 120, 1, 0, 0, 0, 2, 255, 255, 68, 1, 0, 0, 52, 0,
                        0, 0, 1, 0, 40, 0, 0, 0, 52, 0, 0, 0, 52, 0, 1, 0, 36, 0, 0, 0, 52, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
                        0, 0, 0, 0, 0, 0, 0, 0, 2, 255, 255, 81, 0, 0, 5, 1, 0, 15, 160, 0, 0, 0, 0, 0, 0, 0, 63, 0, 0,
                        128, 191, 0, 0, 0, 0, 31, 0, 0, 2, 0, 0, 0, 128, 0, 0, 3, 176, 31, 0, 0, 2, 0, 0, 0, 144, 0, 8,
                        15, 160, 6, 0, 0, 2, 0, 0, 1, 128, 0, 0, 0, 160, 6, 0, 0, 2, 0, 0, 2, 128, 0, 0, 85, 160, 19, 0,
                        0, 2, 0, 0, 12, 128, 0, 0, 27, 176, 2, 0, 0, 3, 1, 0, 3, 128, 0, 0, 27, 128, 1, 0, 170, 160, 4,
                        0, 0, 4, 1, 0, 3, 128, 1, 0, 228, 128, 0, 0, 228, 128, 1, 0, 85, 160, 5, 0, 0, 3, 0, 0, 3, 128,
                        0, 0, 228, 128, 0, 0, 27, 128, 2, 0, 0, 3, 0, 0, 12, 128, 0, 0, 228, 129, 0, 0, 27, 176, 11, 0,
                        0, 3, 1, 0, 12, 128, 0, 0, 27, 128, 1, 0, 0, 160, 10, 0, 0, 3, 0, 0, 3, 128, 1, 0, 27, 128, 1, 0,
                        85, 160, 11, 0, 0, 3, 2, 0, 3, 128, 1, 0, 228, 128, 1, 0, 0, 160, 10, 0, 0, 3, 1, 0, 3, 128, 2,
                        0, 228, 128, 1, 0, 85, 160, 2, 0, 0, 3, 0, 0, 3, 128, 0, 0, 228, 128, 1, 0, 228, 128, 2, 0, 0, 3,
                        0, 0, 3, 128, 0, 0, 228, 128, 0, 0, 27, 128, 6, 0, 0, 2, 1, 0, 1, 128, 0, 0, 170, 160, 6, 0, 0,
                        2, 1, 0, 2, 128, 0, 0, 255, 160, 5, 0, 0, 3, 0, 0, 3, 128, 0, 0, 228, 128, 1, 0, 228, 128, 66, 0,
                        0, 3, 0, 0, 15, 128, 0, 0, 228, 128, 0, 8, 228, 160, 1, 0, 0, 2, 0, 8, 15, 128, 0, 0, 228, 128,
                        255, 255, 0, 0, 83, 72, 68, 82, 188, 1, 0, 0, 64, 0, 0, 0, 111, 0, 0, 0, 89, 0, 0, 4, 70, 142,
                        32, 0, 0, 0, 0, 0, 1, 0, 0, 0, 90, 0, 0, 3, 0, 96, 16, 0, 0, 0, 0, 0, 88, 24, 0, 4, 0, 112, 16,
                        0, 0, 0, 0, 0, 85, 85, 0, 0, 98, 16, 0, 3, 50, 16, 16, 0, 1, 0, 0, 0, 101, 0, 0, 3, 242, 32, 16,
                        0, 0, 0, 0, 0, 104, 0, 0, 2, 1, 0, 0, 0, 26, 0, 0, 5, 50, 0, 16, 0, 0, 0, 0, 0, 70, 16, 16, 0, 1,
                        0, 0, 0, 0, 0, 0, 10, 194, 0, 16, 0, 0, 0, 0, 0, 6, 4, 16, 0, 0, 0, 0, 0, 2, 64, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 128, 191, 0, 0, 128, 191, 14, 0, 0, 8, 242, 0, 16, 0, 0, 0, 0, 0, 70, 14,
                        16, 0, 0, 0, 0, 0, 70, 132, 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 194, 0, 16, 0, 0, 0, 0,
                        0, 166, 14, 16, 0, 0, 0, 0, 0, 2, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 63, 0, 0, 0, 63, 52,
                        0, 0, 10, 242, 0, 16, 0, 0, 0, 0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 2, 64, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 51, 0, 0, 10, 242, 0, 16, 0, 0, 0, 0, 0, 70, 14, 16, 0, 0, 0, 0, 0, 2,
                        64, 0, 0, 0, 0, 0, 63, 0, 0, 0, 63, 0, 0, 0, 63, 0, 0, 0, 63, 0, 0, 0, 7, 50, 0, 16, 0, 0, 0, 0,
                        0, 230, 10, 16, 0, 0, 0, 0, 0, 70, 0, 16, 0, 0, 0, 0, 0, 65, 0, 0, 5, 194, 0, 16, 0, 0, 0, 0, 0,
                        6, 20, 16, 0, 1, 0, 0, 0, 0, 0, 0, 7, 50, 0, 16, 0, 0, 0, 0, 0, 70, 0, 16, 0, 0, 0, 0, 0, 230,
                        10, 16, 0, 0, 0, 0, 0, 14, 0, 0, 8, 50, 0, 16, 0, 0, 0, 0, 0, 70, 0, 16, 0, 0, 0, 0, 0, 230, 138,
                        32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 69, 0, 0, 9, 242, 32, 16, 0, 0, 0, 0, 0, 70, 0, 16, 0, 0, 0, 0, 0,
                        70, 126, 16, 0, 0, 0, 0, 0, 0, 96, 16, 0, 0, 0, 0, 0, 62, 0, 0, 1, 83, 84, 65, 84, 116, 0, 0, 0,
                        12, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 82, 68, 69, 70, 92, 1, 0, 0, 1, 0, 0,
                        0, 172, 0, 0, 0, 3, 0, 0, 0, 28, 0, 0, 0, 0, 4, 255, 255, 0, 129, 0, 0, 38, 1, 0, 0, 124, 0, 0,
                        0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 139, 0, 0,
                        0, 2, 0, 0, 0, 5, 0, 0, 0, 4, 0, 0, 0, 255, 255, 255, 255, 0, 0, 0, 0, 1, 0, 0, 0, 13, 0, 0, 0,
                        160, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
                        103, 109, 95, 66, 97, 115, 101, 84, 101, 120, 116, 117, 114, 101, 0, 103, 109, 95, 66, 97, 115,
                        101, 84, 101, 120, 116, 117, 114, 101, 79, 98, 106, 101, 99, 116, 0, 36, 71, 108, 111, 98, 97,
                        108, 115, 0, 171, 171, 171, 160, 0, 0, 0, 2, 0, 0, 0, 196, 0, 0, 0, 16, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 244, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 2, 0, 0, 0, 8, 1, 0, 0, 0, 0, 0, 0, 24, 1, 0, 0,
                        8, 0, 0, 0, 8, 0, 0, 0, 2, 0, 0, 0, 8, 1, 0, 0, 0, 0, 0, 0, 117, 95, 116, 101, 120, 101, 108,
                        115, 80, 101, 114, 80, 105, 120, 101, 108, 0, 171, 171, 171, 1, 0, 3, 0, 1, 0, 2, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 117, 102, 95, 114, 101, 115, 111, 108, 117, 116, 105, 111, 110, 0, 77, 105, 99, 114,
                        111, 115, 111, 102, 116, 32, 40, 82, 41, 32, 72, 76, 83, 76, 32, 83, 104, 97, 100, 101, 114, 32,
                        67, 111, 109, 112, 105, 108, 101, 114, 32, 57, 46, 51, 48, 46, 57, 50, 48, 48, 46, 49, 54, 51,
                        56, 52, 0, 171, 171, 171, 73, 83, 71, 78, 108, 0, 0, 0, 3, 0, 0, 0, 8, 0, 0, 0, 80, 0, 0, 0, 0,
                        0, 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 15, 0, 0, 0, 92, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3,
                        0, 0, 0, 1, 0, 0, 0, 3, 3, 0, 0, 101, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 2, 0, 0, 0,
                        15, 0, 0, 0, 83, 86, 95, 80, 79, 83, 73, 84, 73, 79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0,
                        67, 79, 76, 79, 82, 0, 171, 79, 83, 71, 78, 44, 0, 0, 0, 1, 0, 0, 0, 8, 0, 0, 0, 32, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 15, 0, 0, 0, 83, 86, 95, 84, 65, 82, 71, 69, 84, 0,
                        171, 171, 103, 109, 95, 66, 97, 115, 101, 84, 101, 120, 116, 117, 114, 101, 0, 103, 109, 95, 66,
                        97, 115, 101, 84, 101, 120, 116, 117, 114, 101, 79, 98, 106, 101, 99, 116, 0, 36, 71, 108, 111,
                        98, 97, 108, 115, 0, 117, 95, 116, 101, 120, 101, 108, 115, 80, 101, 114, 80, 105, 120, 101, 108,
                        0, 117, 102, 95, 114, 101, 115, 111, 108, 117, 116, 105, 111, 110, 0, 83, 86, 95, 80, 79, 83, 73,
                        84, 73, 79, 78, 0, 84, 69, 88, 67, 79, 79, 82, 68, 0, 67, 79, 76, 79, 82, 0, 0
                    ];
                    gmsPlasmaShader.HLSL11_PixelData.IsNull = false;
                }
                gmsShaders.Add(gmsPlasmaShader);

                /// Scripts and Functions
                Console.WriteLine("Rewiring instance deactivation functions...");
                Console.WriteLine();
                CodeImportGroup cig = new(gmsData);
                foreach (string functionName in new string[] { "instance_deactivate_all", "instance_deactivate_object", "instance_deactivate_region", "instance_deactivate_layer" })
                {
                    UndertaleFunction? gmsFunction = gmsData.Functions.ByName(functionName);
                    if (gmsFunction is not null)
                    {
                        UndertaleCode gmsCode = UndertaleCode.CreateEmptyEntry(gmsData, $"gml_Script_{functionName}");
                        cig.QueueAppend(gmsCode, $$"""
                        if (argument_count > 6){
                            {{functionName}}(argument[0], argument[1], argument[2], argument[3], argument[4], argument[5], argument[6]);
                        }
                        else if (argument_count == 6){
                            {{functionName}}(argument[0], argument[1], argument[2], argument[3], argument[4], argument[5]);
                        }
                        else if (argument_count > 1){
                            {{functionName}}(argument[0], argument[1]);
                        }
                        else{
                            {{functionName}}(argument[0]);
                        }
                        instance_activate_object(__SHMOOTH_objImperishable);

                        """);

                        gmsData.Scripts.Add(new()
                        {
                            Name = gmsFunction.Name,
                            Code = gmsCode
                        });
                        gmsFunction.Name.Content = $"__SHMOOTH_{functionName}";
                    }
                }

                /// Objects
                Console.WriteLine("Injecting an imperishable shader control object...");
                Console.WriteLine("(Let's name it ISCO because it sounds epic.)");
                Console.WriteLine();
                UndertaleGameObject gmsImperishableObject = new()
                {
                    Name = gmsStrings.MakeString("__SHMOOTH_objImperishable"),
                    Persistent = true
                };
                gmsData.GameObjects.Add(gmsImperishableObject);

                string defaultSmoothRemovalCode;
                if (!gmsData.IsGameMaker2())
                {
                    defaultSmoothRemovalCode = """
                        texture_set_interpolation(false);

                        """;

                    cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Destroy, gmsData), """
                        if (!__SHMOOTH_isDuplicate){
                            instance_create(0, 0, object_index);
                        }

                        """);
                    cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Draw, EventSubtypeDraw.PostDraw, gmsData), """
                        var __SHMOOTH_windowWidth = window_get_width();
                        var __SHMOOTH_windowHeight = window_get_height();
                        var __SHMOOTH_applicationWidth = surface_get_width(application_surface);
                        var __SHMOOTH_applicationHeight = surface_get_height(application_surface);

                        var __SHMOOTH_aspectRatio = __SHMOOTH_windowWidth / __SHMOOTH_windowHeight;
                        var __SHMOOTH_aspectRatioRatio = __SHMOOTH_aspectRatio / (__SHMOOTH_applicationWidth/__SHMOOTH_applicationHeight);

                        var __SHMOOTH_pixelScalingW = __SHMOOTH_aspectRatioRatio < 1 && __SHMOOTH_windowWidth mod __SHMOOTH_applicationWidth != 0;
                        var __SHMOOTH_pixelScalingH = __SHMOOTH_aspectRatioRatio > 1 && __SHMOOTH_windowHeight mod __SHMOOTH_applicationHeight != 0;
                        var __SHMOOTH_pixelScalingWH = __SHMOOTH_windowWidth mod __SHMOOTH_applicationWidth != 0 && __SHMOOTH_windowHeight mod __SHMOOTH_applicationHeight != 0;
                        var __SHMOOTH_pixelScaling = __SHMOOTH_pixelScalingW || __SHMOOTH_pixelScalingH || __SHMOOTH_pixelScalingWH;

                        texture_set_repeat(false);
                        draw_enable_alphablend(false);

                        if (__SHMOOTH_pixelScaling){
                            texture_set_interpolation(true);
                            shader_set(__SHMOOTH_shPlasma);
                        }  

                        if (__SHMOOTH_aspectRatioRatio < 1){
                            var __SHMOOTH_canvasHeight = __SHMOOTH_windowWidth*__SHMOOTH_applicationHeight/__SHMOOTH_applicationWidth;
                            var __SHMOOTH_vertOutPixels = (__SHMOOTH_windowHeight - __SHMOOTH_canvasHeight) / 2;
                            shader_set_uniform_f(__SHMOOTH_uTexelsPerPixel, __SHMOOTH_applicationWidth/__SHMOOTH_windowWidth, __SHMOOTH_applicationHeight/__SHMOOTH_canvasHeight);
                            shader_set_uniform_f(__SHMOOTH_uvResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                            shader_set_uniform_f(__SHMOOTH_ufResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                            draw_surface_stretched(application_surface, 0, __SHMOOTH_vertOutPixels, __SHMOOTH_windowWidth, __SHMOOTH_canvasHeight);
                        }
                        else{
                            var __SHMOOTH_canvasWidth = __SHMOOTH_windowHeight*__SHMOOTH_applicationWidth/__SHMOOTH_applicationHeight;
                            var __SHMOOTH_horOutPixels = (__SHMOOTH_windowWidth - __SHMOOTH_canvasWidth) / 2;
                            shader_set_uniform_f(__SHMOOTH_uTexelsPerPixel, __SHMOOTH_applicationWidth/__SHMOOTH_canvasWidth, __SHMOOTH_applicationHeight/__SHMOOTH_windowHeight);
                            shader_set_uniform_f(__SHMOOTH_uvResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                            shader_set_uniform_f(__SHMOOTH_ufResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                            draw_surface_stretched(application_surface, __SHMOOTH_horOutPixels, 0, __SHMOOTH_canvasWidth, __SHMOOTH_windowHeight);
                        }

                        if (__SHMOOTH_pixelScaling){
                            shader_reset();
                            texture_set_interpolation(false);
                        }

                        draw_enable_alphablend(true);

                        """);
                }
                else
                {
                    defaultSmoothRemovalCode = """
                        gpu_set_texfilter(false);

                        """;

                    cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Destroy, gmsData), """
                        if (!__SHMOOTH_isDuplicate){
                            instance_create_depth(0, 0, 0, object_index);
                        }

                        """);
                    string gms2PostDraw;
                    if (gmsData.IsVersionAtLeast(2, 3))
                    {
                        gms2PostDraw = """
                            var __SHMOOTH_windowWidth = window_get_width();
                            var __SHMOOTH_windowHeight = window_get_height();
                            var __SHMOOTH_applicationWidth = surface_get_width(application_surface);
                            var __SHMOOTH_applicationHeight = surface_get_height(application_surface);
                        
                            var __SHMOOTH_aspectRatio = __SHMOOTH_windowWidth / __SHMOOTH_windowHeight;
                            var __SHMOOTH_aspectRatioRatio = __SHMOOTH_aspectRatio / (__SHMOOTH_applicationWidth/__SHMOOTH_applicationHeight);
                        
                            var __SHMOOTH_pixelScalingW = __SHMOOTH_aspectRatioRatio < 1 && __SHMOOTH_windowWidth mod __SHMOOTH_applicationWidth != 0;
                            var __SHMOOTH_pixelScalingH = __SHMOOTH_aspectRatioRatio > 1 && __SHMOOTH_windowHeight mod __SHMOOTH_applicationHeight != 0;
                            var __SHMOOTH_pixelScalingWH = __SHMOOTH_windowWidth mod __SHMOOTH_applicationWidth != 0 && __SHMOOTH_windowHeight mod __SHMOOTH_applicationHeight != 0;
                            var __SHMOOTH_pixelScaling = __SHMOOTH_pixelScalingW || __SHMOOTH_pixelScalingH || __SHMOOTH_pixelScalingWH;
                        
                            gpu_set_texrepeat(false);
                            gpu_set_blendenable(false);
                        
                            if (__SHMOOTH_pixelScaling){
                                gpu_set_texfilter(true);
                                shader_set(__SHMOOTH_shPlasma);
                            }  
                        
                            if (__SHMOOTH_aspectRatioRatio < 1){
                                var __SHMOOTH_canvasHeight = __SHMOOTH_windowWidth*__SHMOOTH_applicationHeight/__SHMOOTH_applicationWidth;
                                var __SHMOOTH_vertOutPixels = (__SHMOOTH_windowHeight - __SHMOOTH_canvasHeight) / 2;
                                shader_set_uniform_f(__SHMOOTH_uTexelsPerPixel, __SHMOOTH_applicationWidth/__SHMOOTH_windowWidth, __SHMOOTH_applicationHeight/__SHMOOTH_canvasHeight);
                                shader_set_uniform_f(__SHMOOTH_uvResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                                shader_set_uniform_f(__SHMOOTH_ufResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                                draw_surface_stretched(application_surface, 0, __SHMOOTH_vertOutPixels, __SHMOOTH_windowWidth, __SHMOOTH_canvasHeight);
                            }
                            else{
                                var __SHMOOTH_canvasWidth = __SHMOOTH_windowHeight*__SHMOOTH_applicationWidth/__SHMOOTH_applicationHeight;
                                var __SHMOOTH_horOutPixels = (__SHMOOTH_windowWidth - __SHMOOTH_canvasWidth) / 2;
                                shader_set_uniform_f(__SHMOOTH_uTexelsPerPixel, __SHMOOTH_applicationWidth/__SHMOOTH_canvasWidth, __SHMOOTH_applicationHeight/__SHMOOTH_windowHeight);
                                shader_set_uniform_f(__SHMOOTH_uvResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                                shader_set_uniform_f(__SHMOOTH_ufResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                                draw_surface_stretched(application_surface, __SHMOOTH_horOutPixels, 0, __SHMOOTH_canvasWidth, __SHMOOTH_windowHeight);
                            }
                        
                            if (__SHMOOTH_pixelScaling){
                                shader_reset();
                                gpu_set_texfilter(false);
                            }
                        
                            gpu_set_blendenable(true);

                            """;
                    }
                    else
                    {
                        gms2PostDraw = """
                            var __SHMOOTH_windowWidth = window_get_width();
                            var __SHMOOTH_windowHeight = window_get_height();
                            var __SHMOOTH_guiWidth = display_get_gui_width();
                            var __SHMOOTH_guiHeight = display_get_gui_height();
                            var __SHMOOTH_applicationWidth = surface_get_width(application_surface);
                            var __SHMOOTH_applicationHeight = surface_get_height(application_surface);
                        
                            var __SHMOOTH_aspectRatio = __SHMOOTH_windowWidth / __SHMOOTH_windowHeight;
                            var __SHMOOTH_aspectRatioRatio = __SHMOOTH_aspectRatio / (__SHMOOTH_applicationWidth/__SHMOOTH_applicationHeight);
                        
                            var __SHMOOTH_pixelScalingW = __SHMOOTH_aspectRatioRatio < 1 && __SHMOOTH_windowWidth mod __SHMOOTH_applicationWidth != 0;
                            var __SHMOOTH_pixelScalingH = __SHMOOTH_aspectRatioRatio > 1 && __SHMOOTH_windowHeight mod __SHMOOTH_applicationHeight != 0;
                            var __SHMOOTH_pixelScalingWH = __SHMOOTH_windowWidth mod __SHMOOTH_applicationWidth != 0 && __SHMOOTH_windowHeight mod __SHMOOTH_applicationHeight != 0;
                            var __SHMOOTH_pixelScaling = __SHMOOTH_pixelScalingW || __SHMOOTH_pixelScalingH || __SHMOOTH_pixelScalingWH;
                        
                            gpu_set_texrepeat(false);
                            gpu_set_blendenable(false);
                        
                            if (__SHMOOTH_pixelScaling){
                                gpu_set_texfilter(true);
                                shader_set(__SHMOOTH_shPlasma);
                            }  
                        
                            if (__SHMOOTH_aspectRatioRatio < 1){
                                var __SHMOOTH_canvasHeight = __SHMOOTH_windowWidth*__SHMOOTH_applicationHeight/__SHMOOTH_applicationWidth;
                                var __SHMOOTH_canvasHeightStretched = __SHMOOTH_guiWidth*__SHMOOTH_applicationHeight/__SHMOOTH_applicationWidth;
                                var __SHMOOTH_vertOutPixels = (__SHMOOTH_guiHeight - __SHMOOTH_canvasHeightStretched) / 2;
                                shader_set_uniform_f(__SHMOOTH_uTexelsPerPixel, __SHMOOTH_applicationWidth/__SHMOOTH_windowWidth, __SHMOOTH_applicationHeight/__SHMOOTH_canvasHeight);
                                shader_set_uniform_f(__SHMOOTH_uvResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                                shader_set_uniform_f(__SHMOOTH_ufResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                                draw_surface_stretched(application_surface, 0, __SHMOOTH_vertOutPixels, __SHMOOTH_guiWidth, __SHMOOTH_canvasHeightStretched);
                            }
                            else{
                                var __SHMOOTH_canvasWidth = __SHMOOTH_windowHeight*__SHMOOTH_applicationWidth/__SHMOOTH_applicationHeight;
                                var __SHMOOTH_canvasWidthStretched = __SHMOOTH_guiHeight*__SHMOOTH_applicationWidth/__SHMOOTH_applicationHeight;
                                var __SHMOOTH_horOutPixels = (__SHMOOTH_guiWidth - __SHMOOTH_canvasWidthStretched) / 2;
                                shader_set_uniform_f(__SHMOOTH_uTexelsPerPixel, __SHMOOTH_applicationWidth/__SHMOOTH_canvasWidth, __SHMOOTH_applicationHeight/__SHMOOTH_windowHeight);
                                shader_set_uniform_f(__SHMOOTH_uvResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                                shader_set_uniform_f(__SHMOOTH_ufResolution, __SHMOOTH_applicationWidth, __SHMOOTH_applicationHeight);
                                draw_surface_stretched(application_surface, __SHMOOTH_horOutPixels, 0, __SHMOOTH_canvasWidthStretched, __SHMOOTH_guiHeight);
                            }
                        
                            if (__SHMOOTH_pixelScaling){
                                shader_reset();
                                gpu_set_texfilter(false);
                            }
                        
                            gpu_set_blendenable(true);

                            """;
                    }

                    cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Draw, EventSubtypeDraw.PostDraw, gmsData), gms2PostDraw);
                }
                cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Create, gmsData), """
                    __SHMOOTH_isDuplicate = false;
                    __SHMOOTH_uTexelsPerPixel = shader_get_uniform(__SHMOOTH_shPlasma, "u_texelsPerPixel");
                    __SHMOOTH_uvResolution = shader_get_uniform(__SHMOOTH_shPlasma, "uv_resolution");
                    __SHMOOTH_ufResolution = shader_get_uniform(__SHMOOTH_shPlasma, "uf_resolution");

                    """);
                cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Other, EventSubtypeOther.RoomStart, gmsData), """
                    if (instance_number(object_index) > 1){
                        __SHMOOTH_isDuplicate = true;
                        instance_destroy();
                    }

                    """);
                cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Draw, EventSubtypeDraw.PreDraw, gmsData), defaultSmoothRemovalCode);
                cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawBegin, gmsData), defaultSmoothRemovalCode);
                cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Draw, EventSubtypeDraw.Draw, gmsData), defaultSmoothRemovalCode);
                cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawEnd, gmsData), defaultSmoothRemovalCode + """
                    application_surface_draw_enable(false);

                    """);
                cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUIBegin, gmsData), defaultSmoothRemovalCode);
                cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUI, gmsData), defaultSmoothRemovalCode);
                cig.QueueAppend(gmsImperishableObject.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUIEnd, gmsData), defaultSmoothRemovalCode);
                cig.Import();

                /// Rooms
                Console.WriteLine("Adding the ISCO to the first room...");
                Console.WriteLine();
                IList<UndertaleRoom> gmsRooms = gmsData.Rooms;
                if (gmsRooms.Count < 1)
                {
                    Console.Error.WriteLine("No rooms found in the game.");
                    Console.ReadKey();
                    return;
                }

                gmsRooms[0].GameObjects.Add(new()
                {
                    ObjectDefinition = gmsImperishableObject,
                    InstanceID = gmsData.GeneralInfo.LastObj++
                });

                /// Recompile
                Console.WriteLine("Recompiling the game data...");
                Console.WriteLine();
                File.Move(dataWinFilePath, Path.ChangeExtension(dataWinFilePath, ".backup.win"), true);
                FileStream writeStream = new(dataWinFilePath, FileMode.Create);
                UndertaleIO.Write(writeStream, gmsData);
                writeStream.Dispose();

                if (deleteOriginalGameFile)
                {
                    Console.WriteLine("Deleting the game's original .exe file...");
                    Console.WriteLine();
                    File.Delete(gameFilePath);
                }

                Console.WriteLine("GMShmooth has applied Shmoothing successfully.");
                Console.WriteLine("You can close this window now, enjoy your new 20/20 vision!");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("An error occurred!");
                Console.Error.WriteLine(ex.GetType() + ":");
                Console.Error.WriteLine(ex.Message);
                Console.ReadKey();
            }
        }
    }
}
