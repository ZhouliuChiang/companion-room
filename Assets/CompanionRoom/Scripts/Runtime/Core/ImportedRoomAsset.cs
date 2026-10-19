using UnityEngine;

namespace CompanionRoom
{
    /// <summary>Identity of a supplied prop; interaction binding happens after the layout is approved.</summary>
    [DisallowMultipleComponent]
    public sealed class ImportedRoomAsset : MonoBehaviour
    {
        public string assetId;
        public string sourceFile;
        [TextArea] public string placementNote = "可自由移動、旋轉、縮放；擺好後再綁定互動功能。";
    }
}
