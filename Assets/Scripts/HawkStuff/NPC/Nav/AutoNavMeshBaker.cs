using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class RuntimeNavMeshBuilder : MonoBehaviour
{
    public Bounds bounds = new Bounds(Vector3.zero, new Vector3(500, 20, 500)); // Define navmesh area
    public LayerMask layerMask = ~0; // Everything by default

    void Start()
    {
        BuildRuntimeNavMesh();
    }

    void BuildRuntimeNavMesh()
    {
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(null, layerMask, NavMeshCollectGeometry.RenderMeshes, 0, new List<NavMeshBuildMarkup>(), sources);

        NavMeshData navMeshData = new NavMeshData();
        NavMesh.AddNavMeshData(navMeshData);
        NavMeshBuilder.UpdateNavMeshData(navMeshData, NavMesh.GetSettingsByID(0), sources, bounds);
    }
}
