using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace XiuXian.Editor
{
    /// <summary>Imports only the dedicated V3 art folder; never touches gameplay or other art.</summary>
    public static class ImmortalUIArtImporter
    {
        private const string Root = "Assets/Art/Resources/UI/ImmortalV3/";
        [Serializable] private class Sheet { public string id; public int columns; public int rows; public string[] names; public int[] xCuts; public int[] yCuts; }
        [Serializable] private class ImportPlan { public Sheet[] sheets; }

        [MenuItem("仙途/美术/导入完整 UI 美术资源")]
        public static void ImportAll()
        {
            var plan = JsonUtility.FromJson<ImportPlan>(File.ReadAllText(Root + "import-plan.json"));
            var sheets = new Dictionary<string, Sheet>();
            foreach (var sheet in plan.sheets) sheets.Add(sheet.id, sheet);
            foreach (string file in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories))
            {
                string path = file.Replace('\\','/');
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("No texture importer: "+path);
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 100;
                importer.isReadable = false;
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                if (sheets.TryGetValue(Path.GetFileNameWithoutExtension(path),out var sheet))
                {
                    importer.spriteImportMode=SpriteImportMode.Multiple;
                    var sprites=new SpriteMetaData[sheet.names.Length];
                    for(int i=0;i<sprites.Length;i++)
                    {
                        float x0=sheet.xCuts != null && sheet.xCuts.Length == sheet.columns+1 ? sheet.xCuts[i%sheet.columns] : Mathf.Round((i%sheet.columns)*width/(float)sheet.columns);
                        float x1=sheet.xCuts != null && sheet.xCuts.Length == sheet.columns+1 ? sheet.xCuts[i%sheet.columns+1] : Mathf.Round((i%sheet.columns+1)*width/(float)sheet.columns);
                        float y0=sheet.yCuts != null && sheet.yCuts.Length == sheet.rows+1 ? sheet.yCuts[i/sheet.columns] : Mathf.Round((i/sheet.columns)*height/(float)sheet.rows);
                        float y1=sheet.yCuts != null && sheet.yCuts.Length == sheet.rows+1 ? sheet.yCuts[i/sheet.columns+1] : Mathf.Round((i/sheet.columns+1)*height/(float)sheet.rows);
                        sprites[i]=new SpriteMetaData {name=sheet.names[i],rect=new Rect(x0,height-y1,x1-x0,y1-y0),pivot=new Vector2(.5f,.5f),alignment=0};
                    }
                    #pragma warning disable 0618
                    importer.spritesheet=sprites;
                    #pragma warning restore 0618
                }
                else importer.spriteImportMode=SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Immortal UI V3 artwork imported. Gameplay scenes and code unchanged.");
        }
    }
}
