using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PathfindingLinkMonoBehaviour))]
public class PathFindingLinkMonoBehaviourEditor : Editor
{
    private void OnSceneGUI()
    {
        PathfindingLinkMonoBehaviour pathfindingLinkMonoBehaviour = (PathfindingLinkMonoBehaviour)target;


        EditorGUI.BeginChangeCheck();
        Vector3 newLinkPositonA = Handles.PositionHandle(pathfindingLinkMonoBehaviour.linkPositionA, Quaternion.identity);
        Vector3 newLinkPositonB = Handles.PositionHandle(pathfindingLinkMonoBehaviour.linkPositionB, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        { 
            pathfindingLinkMonoBehaviour.linkPositionA = newLinkPositonA;
            pathfindingLinkMonoBehaviour.linkPositionB = newLinkPositonB;
        }
    }
}
