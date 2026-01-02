using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class CCTVOcclusionSorter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SortingGroup npcGroup;
    [SerializeField] private SortingGroup occluderGroup; // desk/cubicle SortingGroup
    [SerializeField] private Transform gateY;            // threshold line (Y)

    [Header("Order = occluderOrder + delta")]
    [SerializeField] private int frontDelta = 1;   // npc in front of desk/cubicle
    [SerializeField] private int behindDelta = -1; // npc behind desk/cubicle

    private void Reset()
    {
        npcGroup = GetComponent<SortingGroup>();
    }

    private void LateUpdate()
    {
        if (npcGroup == null || occluderGroup == null || gateY == null) return;

        bool behind = transform.position.y > gateY.position.y;
        int baseOrder = occluderGroup.sortingOrder;

        npcGroup.sortingOrder = baseOrder + (behind ? behindDelta : frontDelta);
    }
}
