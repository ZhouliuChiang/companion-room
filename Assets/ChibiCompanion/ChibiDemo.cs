using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class ChibiDemo : MonoBehaviour
{
    public Animator character;
    public Transform displayRoot;
    public Camera viewCamera;
    public string currentAction = "Idle";
    private float yaw;
    private float speed = 1;
    public void Play(string action)
    {
        currentAction = action;
        character.CrossFadeInFixedTime(action, .15f, 0, 0);
    }
    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.digit1Key.wasPressedThisFrame) Play("Idle");
            if (k.digit2Key.wasPressedThisFrame) Play("Wave");
            if (k.digit3Key.wasPressedThisFrame) Play("Walk");
            if (k.digit4Key.wasPressedThisFrame) Play("Run");
            if (k.leftArrowKey.isPressed) yaw -= Time.deltaTime * 65;
            if (k.rightArrowKey.isPressed) yaw += Time.deltaTime * 65;
        }
#endif
        character.speed = speed;
        if (displayRoot) displayRoot.localRotation = Quaternion.Euler(0, yaw, 0);
    }
    private void OnGUI()
    {
        float scale = Mathf.Max(1, Screen.height / 850f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUILayout.BeginArea(new Rect(18, 18, 260, 360), GUI.skin.box);
        GUILayout.Label("CHIBI COMPANION / Q版動作");
        GUILayout.Space(8);
        if (GUILayout.Button("1  待機 / Idle", GUILayout.Height(36))) Play("Idle");
        if (GUILayout.Button("2  揮手 / Wave", GUILayout.Height(36))) Play("Wave");
        if (GUILayout.Button("3  走路 / Walk", GUILayout.Height(36))) Play("Walk");
        if (GUILayout.Button("4  跑步 / Run", GUILayout.Height(36))) Play("Run");
        GUILayout.Label("速度 / Speed"); speed = GUILayout.HorizontalSlider(speed, .25f, 1.5f);
        GUILayout.Label("旋轉 / Rotate"); yaw = GUILayout.HorizontalSlider(yaw, -180, 180);
        if (GUILayout.Button("正面 / Front")) yaw = 0;
        GUILayout.Label("小步幅・固定頭髮・原地動作");
        GUILayout.EndArea();
    }
}
