using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace CompanionRoom.EditorTools
{
 /// <summary>
 /// Puts the pink witch (Art/Witch/PinkWitch.fbx, Walk + Idle clips) into the room scene in the open
 /// editor. Create Temp/WitchSetup.request or use the menu. Result: Artifacts/Witch/setup.txt
 /// (or failed.txt).
 /// </summary>
 [InitializeOnLoad]
 public static class WitchSetup
 {
  const string Folder="Assets/CompanionRoom/Art/Witch";
  const string Request="Temp/WitchSetup.request";
  static WitchSetup()=>EditorApplication.update+=Poll;
  [MenuItem("Tools/兔兔房間/重新放置粉色女巫",priority=40)]
  static void Menu()=>File.WriteAllText(Request,"setup");
  const string RemoveRequest="Temp/WitchRemove.request";
  static void Poll()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(File.Exists(RemoveRequest)){File.Delete(RemoveRequest);Remove();return;}
   if(!File.Exists(Request))return;
   File.Delete(Request);Apply();
  }
  /// <summary>Takes the witch back out of the room scene and deletes her generated prefab.</summary>
  public static void Remove()
  {
   try
   {
    var scene=EditorSceneManager.GetActiveScene();
    if(scene.path!=UserModelWorkspace.ScenePath)scene=EditorSceneManager.OpenScene(UserModelWorkspace.ScenePath);
    var app=Object.FindFirstObjectByType<CompanionRoomApp>();
    if(app!=null){var so=new SerializedObject(app);var p=so.FindProperty("companion");if(p.objectReferenceValue is RoomCompanion c&&c.GetComponent<WitchLocomotion>()!=null){p.objectReferenceValue=null;so.ApplyModifiedPropertiesWithoutUndo();}}
    int removed=0;foreach(var w in Object.FindObjectsByType<WitchLocomotion>(FindObjectsInactive.Include,FindObjectsSortMode.None)){Object.DestroyImmediate(w.gameObject);removed++;}
    var marker=GameObject.Find("點擊位置標記");if(marker!=null)Object.DestroyImmediate(marker);
    AssetDatabase.DeleteAsset("Assets/CompanionRoom/Prefabs/粉色女巫.prefab");
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    File.WriteAllText("Artifacts/Witch/removed.txt","Removed "+removed+" witch object(s); companion link cleared; prefab deleted.\n");
   }catch(Exception e){File.WriteAllText("Artifacts/Witch/failed.txt",e.ToString());Debug.LogException(e);}
  }
  public static void Apply()
  {
   Directory.CreateDirectory("Artifacts/Witch");File.Delete("Artifacts/Witch/failed.txt");
   try
   {
    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    string modelPath=Folder+"/PinkWitch.fbx";
    var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;
    // The skirt rig (WitchSkirt) re-weights the mesh at runtime, which needs readable mesh data in the player.
    importer.isReadable=true;
    var clips=importer.defaultClipAnimations;
    foreach(var clip in clips){clip.name=clip.takeName.Contains("Walk")?"Walk":"Idle";clip.loopTime=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;clip.lockRootRotation=true;}
    importer.clipAnimations=clips;importer.SaveAndReimport();
    var shader=Shader.Find("Universal Render Pipeline/Lit");var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Witch.mat");
    if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,Folder+"/Witch.mat");}mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/WitchBase.png"));mat.SetFloat("_Smoothness",.22f);EditorUtility.SetDirty(mat);
    var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Witch.controller");
    if(ctrl==null)ctrl=AnimatorController.CreateAnimatorControllerAtPath(Folder+"/Witch.controller");
    var sm=ctrl.layers[0].stateMachine;foreach(var state in sm.states)sm.RemoveState(state.state);
    var motions=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
    foreach(var motion in motions){var state=sm.AddState(motion.name);state.motion=motion;if(motion.name=="Idle")sm.defaultState=state;}
    if(!motions.Any(c=>c.name=="Walk")||!motions.Any(c=>c.name=="Idle"))throw new Exception("Missing clips: "+string.Join(",",motions.Select(c=>c.name)));
    // Work in the open room scene; only open it if something else is open.
    var scene=EditorSceneManager.GetActiveScene();
    if(scene.path!=UserModelWorkspace.ScenePath){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())throw new Exception("Scene switch cancelled");scene=EditorSceneManager.OpenScene(UserModelWorkspace.ScenePath);}
    if(!File.Exists("Artifacts/Witch/BeforeWitch.unity"))File.Copy(UserModelWorkspace.ScenePath,"Artifacts/Witch/BeforeWitch.unity",true);
    var old=GameObject.Find("粉色女巫");if(old!=null)Object.DestroyImmediate(old);
    var root=new GameObject("粉色女巫");var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),root.transform);
    // Face the root's +Z (the walker steers the root): forward is where the toes point in the bind pose.
    Transform Bone(string n)=>model.GetComponentsInChildren<Transform>().First(t=>t.name==n);
    var toes=(Bone("Left_Toes").position+Bone("Right_Toes").position)*.5f-(Bone("Left_Foot").position+Bone("Right_Foot").position)*.5f;toes.y=0;
    float turn=Vector3.SignedAngle(toes,Vector3.forward,Vector3.up);model.transform.rotation=Quaternion.AngleAxis(turn,Vector3.up)*model.transform.rotation;
    var renderers=model.GetComponentsInChildren<Renderer>();Bounds Measure(){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
    var bounds=Measure();float scale=1.35f/bounds.size.y;model.transform.localScale*=scale;
    bounds=Measure();model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
    foreach(var r in renderers)r.sharedMaterial=mat;
    foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())r.updateWhenOffscreen=true;
    var animator=model.GetComponentInChildren<Animator>();if(animator==null)animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=ctrl;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    var companion=root.AddComponent<RoomCompanion>();var walker=root.AddComponent<WitchLocomotion>();
    root.AddComponent<WitchSkirt>();root.AddComponent<WitchBodyMotion>();root.AddComponent<WitchClickMarker>();
    var room=Object.FindObjectsByType<ImportedRoomAsset>(FindObjectsSortMode.None).First(p=>p.assetId=="Room");
    var mesh=room.GetComponentInChildren<MeshCollider>();if(mesh==null)throw new Exception("Room needs its mesh collider");walker.roomBounds=mesh.bounds;
    // Footprint: hat brim, skirt and hanging hands, measured from the model, plus room for the skirt to flare.
    float reach=0;foreach(var r in renderers){var b=r.bounds;reach=Mathf.Max(reach,b.extents.x,b.extents.z);}
    walker.height=1.35f;
    Physics.SyncTransforms();bool found=false;
    for(float z=1.5f;z>-2.5f&&!found;z-=.15f)for(float x=.8f;x>-2.5f;x-=.15f)
    {var p=new Vector3(x,walker.floor,z);root.transform.position=p;Physics.SyncTransforms();if(walker.Clear(p)){found=true;break;}}
    if(!found)throw new Exception("No collision-free spawn available");
    var app=Object.FindFirstObjectByType<CompanionRoomApp>();var so=new SerializedObject(app);so.FindProperty("companion").objectReferenceValue=companion;so.ApplyModifiedPropertiesWithoutUndo();
    // Dedicated skirt safety volume also exposes the protected region for scene debugging.
    var skirt=new GameObject("裙擺碰撞保護範圍");skirt.transform.SetParent(root.transform,false);var capsule=skirt.AddComponent<CapsuleCollider>();capsule.center=new Vector3(0,.39f,0);capsule.radius=.30f;capsule.height=.60f;capsule.isTrigger=true;
    var body=root.AddComponent<CapsuleCollider>();body.center=new Vector3(0,walker.height*.5f,0);body.radius=walker.radius;body.height=walker.height;body.isTrigger=false;
    Directory.CreateDirectory("Assets/CompanionRoom/Prefabs");
    PrefabUtility.SaveAsPrefabAssetAndConnect(root,"Assets/CompanionRoom/Prefabs/粉色女巫.prefab",InteractionMode.AutomatedAction);
    AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    string report="Spawn "+root.transform.position+"\nModel turned "+turn.ToString("0.0")+"° to face +Z\nRoom "+walker.roomBounds+"\nT-pose half width "+reach.ToString("0.000")+"  footprint radius "+walker.radius+"\nClips "+string.Join(",",motions.Select(c=>c.name))+"\n";
    report+=Verify(walker);
    File.WriteAllText("Artifacts/Witch/setup.txt",report);Debug.Log("[WitchSetup] "+report);
   }catch(Exception e){File.WriteAllText("Artifacts/Witch/failed.txt",e.ToString());Debug.LogException(e);}
  }
  static string Verify(WitchLocomotion walker)
  {
   int reachable=0,rejected=0;
   for(float x=-2;x<=2;x+=.3f)for(float z=-2;z<=2;z+=.3f){if(walker.GoTo(new Vector3(x,walker.floor,z)))reachable++;else rejected++;walker.Stop();}
   if(reachable==0)throw new Exception("No reachable destinations");
   if(walker.GoTo(new Vector3(100,walker.floor,100)))throw new Exception("Out-of-room destination accepted");
   walker.Stop();
   // Map of where she can stand (rows = z from back to front, columns = x), and what blocks the rest.
   var map=new System.Text.StringBuilder();var blockers=new System.Collections.Generic.Dictionary<string,int>();
   var b=walker.roomBounds;
   for(float z=b.max.z-.1f;z>b.min.z;z-=.15f)
   {
    for(float x=b.min.x+.1f;x<b.max.x;x+=.15f)
    {
     char c=walker.Diagnose(new Vector3(x,walker.floor,z),out var hit);map.Append(c==' '?'.':c);
     if(hit!=null){string k=c+" "+hit.name+" ("+(hit.GetComponentInParent<ImportedRoomAsset>()?.assetId??"?")+")";blockers[k]=blockers.TryGetValue(k,out int v)?v+1:1;}
    }
    map.AppendLine();
   }
   File.WriteAllText("Artifacts/Witch/walkmap.txt",map+"\n"+string.Join("\n",blockers.OrderByDescending(kv=>kv.Value).Select(kv=>kv.Value+"  "+kv.Key)));
   return "Reachable test destinations: "+reachable+"; blocked: "+rejected+"\nOutside room rejected.\n";
  }
 }
}
