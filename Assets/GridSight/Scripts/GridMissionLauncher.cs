using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GridMissionLauncher : MonoBehaviour
{
    [SerializeField] private string droneScene = "SampleScene";

    public void LaunchDroneMission()
    {
        if (!Application.CanStreamedLevelBeLoaded(droneScene)) {
            Debug.LogError($"GridSight could not load drone scene '{droneScene}'. Add it to Build Settings.", this);
            return;
        }

        Debug.Log($"GridSight loading drone mission scene '{droneScene}'.", this);
        SceneManager.LoadScene(droneScene);
    }
}
