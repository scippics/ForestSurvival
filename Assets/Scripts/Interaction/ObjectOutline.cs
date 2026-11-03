using System.Collections.Generic;
using UnityEngine;

// Рисует кайму вокруг деталей выбранного объекта. Материалы самих деталей не трогает,
// поэтому снять подсветку можно просто перестав рисовать.
[RequireComponent(typeof(Camera))]
public class ObjectOutline : MonoBehaviour
{
    static Material material;

    readonly List<MeshRenderer> parts = new List<MeshRenderer>();
    readonly List<Mesh> meshes = new List<Mesh>();
    Camera view;
    Transform target;
    string onlyNamed;

    void Awake()
    {
        view = GetComponent<Camera>();
        if (material == null)
            material = new Material(Resources.Load<Shader>("ForestOutline"));
    }

    // У машины подсвечивается только дверь: кайма по всему кузову с салоном была бы кашей.
    public void Show(Transform root, string partName = null)
    {
        if (root == target && partName == onlyNamed)
            return;

        target = root;
        onlyNamed = partName;
        parts.Clear();
        meshes.Clear();
        if (root == null)
            return;

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (partName != null && filter.name != partName)
                continue;
            if (filter.sharedMesh != null && filter.TryGetComponent(out MeshRenderer part))
            {
                parts.Add(part);
                meshes.Add(filter.sharedMesh);
            }
        }
    }

    void LateUpdate()
    {
        for (int i = 0; i < parts.Count; i++)
        {
            MeshRenderer part = parts[i];
            if (part == null || !part.enabled || !part.gameObject.activeInHierarchy)
                continue;
            for (int sub = 0; sub < meshes[i].subMeshCount; sub++)
                Graphics.DrawMesh(meshes[i], part.localToWorldMatrix, material, 0, view, sub, null, false, false, false);
        }
    }
}
