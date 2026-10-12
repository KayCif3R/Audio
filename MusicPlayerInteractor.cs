using UnityEngine;

public class MusicPlayerInteractor : MonoBehaviour
{
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private LayerMask hitLayers = ~0; // Everything

    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitLayers))
            {
                var player = hit.collider.GetComponentInParent<MusicPlayer>();
                if (player != null)
                    player.PlayPause();
            }
        }
    }
}