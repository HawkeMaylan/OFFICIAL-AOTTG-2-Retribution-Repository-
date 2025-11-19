using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshDecalProjector: MonoBehaviour
{
    public float decalSize = 1f;
    public LayerMask affectedLayers;
    public Material decalMaterial;

    void Start()
    {
        ProjectDecal();
    }

    void ProjectDecal()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        Collider[] targets = Physics.OverlapBox(transform.position, Vector3.one * decalSize * 0.5f, transform.rotation, affectedLayers);
        int triOffset = 0;

        foreach (Collider target in targets)
        {
            Mesh targetMesh = target.GetComponent<MeshFilter>()?.sharedMesh;
            if (targetMesh == null) continue;

            Transform targetTransform = target.transform;
            Vector3[] targetVerts = targetMesh.vertices;
            int[] targetTris = targetMesh.triangles;

            for (int i = 0; i < targetTris.Length; i += 3)
            {
                Vector3 v0 = targetTransform.TransformPoint(targetVerts[targetTris[i]]);
                Vector3 v1 = targetTransform.TransformPoint(targetVerts[targetTris[i + 1]]);
                Vector3 v2 = targetTransform.TransformPoint(targetVerts[targetTris[i + 2]]);

                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                Vector3 center = (v0 + v1 + v2) / 3f;
                Vector3 dir = transform.forward;

                ///if (Vector3.Dot(normal, -dir) < 0.5f) continue;
                if (Vector3.Distance(transform.position, center) > decalSize) continue;

                vertices.Add(transform.InverseTransformPoint(v0));
                vertices.Add(transform.InverseTransformPoint(v1));
                vertices.Add(transform.InverseTransformPoint(v2));



                triangles.Add(triOffset++);
                triangles.Add(triOffset++);
                triangles.Add(triOffset++);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);

        filter.mesh = mesh;
        GetComponent<MeshRenderer>().material = decalMaterial;
    }
}
