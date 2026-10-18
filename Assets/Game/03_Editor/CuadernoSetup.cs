using UnityEditor;
using UnityEngine;

namespace Gamejam2026.EditorTools
{
    // Ajustes de importación del cuaderno; se puede volver a ejecutar sin efectos secundarios.
    public static class CuadernoSetup
    {
        const string Folder = "Assets/Game/07_Cuaderno/Resources/Cuaderno/";

        [MenuItem("Gamejam/Cuaderno/Ajustar importación")]
        public static string Apply()
        {
            int changed = 0;
            if (AssetImporter.GetAtPath(Folder + "binder_notebook_nor_gl_2k.jpg") is TextureImporter normal && normal.textureType != TextureImporterType.NormalMap)
            {
                normal.textureType = TextureImporterType.NormalMap;
                normal.SaveAndReimport();
                changed++;
            }
            if (AssetImporter.GetAtPath(Folder + "binder_notebook_arm_2k.jpg") is TextureImporter arm && arm.sRGBTexture)
            {
                arm.sRGBTexture = false;
                arm.SaveAndReimport();
                changed++;
            }
            foreach (var name in new[] { "binder_closed", "binder_body", "binder_cover" })
            {
                if (!(AssetImporter.GetAtPath(Folder + name + ".fbx") is ModelImporter model)) continue;
                bool dirty = model.materialImportMode != ModelImporterMaterialImportMode.None || model.importCameras || model.importLights || model.importAnimation;
                if (!dirty) continue;
                model.materialImportMode = ModelImporterMaterialImportMode.None;
                model.importCameras = false;
                model.importLights = false;
                model.importAnimation = false;
                model.SaveAndReimport();
                changed++;
            }
            return "cambios=" + changed;
        }
    }
}
