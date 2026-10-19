using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CompanionRoom.EditorTools
{
    /// <summary>One-shot request to run the import in the already-open project editor.</summary>
    [InitializeOnLoad]
    public static class UserModelImportRequest
    {
        const string Request = "Temp/UserModelImport.request";
        const string AdditionalRequest = "Temp/UserModelAdditional.request";
        static UserModelImportRequest() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(AdditionalRequest))
            {
                File.Delete(AdditionalRequest);
                try { UserModelWorkspace.AppendAdditionalModels(); }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                    File.WriteAllText("Artifacts/UserModelsImport/additional_import_failed.txt", e.ToString());
                }
                return;
            }
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            try
            {
                EditorSceneManager.SaveOpenScenes();
                UserModelWorkspace.ImportAndCreateWorkspace();
                UserModelWorkspace.OpenWorkspace();
                File.WriteAllText("Artifacts/UserModelsImport/editor_import_complete.txt", "Imported into the existing Unity editor; new scene active; no character.\n");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                File.WriteAllText("Artifacts/UserModelsImport/editor_import_failed.txt", e.ToString());
            }
        }
    }
}
