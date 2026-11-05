using System.Collections.Generic;
using UnityEngine;

// Готовая модель со скелетом вместо фигуры из примитивов. Шарниры остаются: их, как и раньше, крутят
// походка и поворот головы, а кости каждый кадр повторяют эти повороты после всех остальных скриптов.
[DefaultExecutionOrder(100)]
public class SkinnedCharacter : MonoBehaviour
{
    const string BonePrefix = "mixamorig:";
    const float SkinSmoothness = 0.2f;
    const float ClothSmoothness = 0.05f;
    const float BootSmoothness = 0.3f;
    const float EyeSmoothness = 0.6f;
    const int SurfaceCloth = 9;

    // Текстуры кожи рассчитаны на студийный свет: в сумрачном лесу без приглушения лицо горело бы белым.
    static readonly Color SkinTint = new Color(0.6f, 0.55f, 0.53f);
    static readonly Color EyeTint = new Color(0.8f, 0.8f, 0.8f);

    struct Joint
    {
        public Transform Pivot;
        public Transform Bone;
        public Quaternion Rest;
    }

    readonly List<Joint> joints = new List<Joint>();

    // Без файлов модели фигура остаётся прежней.
    public static bool TryApply(Transform mannequin, string name, float feetHeight)
    {
        string folder = "Characters/" + name + "/";
        var source = Resources.Load<GameObject>(folder + name);
        if (source == null)
            return false;

        foreach (Renderer part in mannequin.GetComponentsInChildren<Renderer>())
        {
            GameObject owner = part.gameObject;
            DestroyImmediate(part);
            if (owner.TryGetComponent(out MeshFilter filter))
                DestroyImmediate(filter);
        }

        GameObject model = Instantiate(source, mannequin, false);
        model.name = "Model";
        model.transform.localPosition = Vector3.up * feetHeight;

        foreach (SkinnedMeshRenderer part in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            // У тела отдельные слоты под губы, уши и ногти, а текстура на всех одна.
            var slots = new Material[part.sharedMaterials.Length];
            System.Array.Fill(slots, Dress(part.name, folder, name));
            part.sharedMaterials = slots;
            part.updateWhenOffscreen = true;
        }

        mannequin.gameObject.AddComponent<SkinnedCharacter>().Bind(mannequin, model.transform);
        return true;
    }

    static Material Dress(string part, string folder, string name)
    {
        switch (part)
        {
            case "Body":
                return Textured(folder + name + "_Skin", SkinSmoothness, SkinTint);
            case "Jacket":
                return Textured(folder + name + "_Jacket", ClothSmoothness, Color.white);
            case "Trousers":
                return Textured(folder + name + "_Trousers", ClothSmoothness, Color.white);
            case "Shoes":
                return Textured(folder + name + "_Boots", BootSmoothness, Color.white);
            case "Eyes":
                return Textured(folder + name + "_Eye", EyeSmoothness, EyeTint);
            case "Hair":
                return Textured(folder + name + "_Hair", ClothSmoothness, Color.white);
            case "Boots":
                return Plain(new Color(0.03f, 0.03f, 0.028f), BootSmoothness, 0);
            case "HatFur":
                return Plain(new Color(0.26f, 0.19f, 0.12f), ClothSmoothness, SurfaceCloth);
            case "Collar":
                return Plain(new Color(0.2f, 0.185f, 0.125f), ClothSmoothness, SurfaceCloth);
            default:
                return Plain(new Color(0.14f, 0.15f, 0.11f), ClothSmoothness, SurfaceCloth);
        }
    }

    static Material Textured(string path, float smoothness, Color tint)
    {
        Material material = ProceduralModels.CreateLit(tint);
        material.mainTexture = Resources.Load<Texture2D>(path);
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    static Material Plain(Color color, float smoothness, int surface)
    {
        Material material = ProceduralModels.CreateLit(color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Surface", surface);
        return material;
    }

    void Bind(Transform mannequin, Transform model)
    {
        var bones = new Dictionary<string, Transform>();
        foreach (Transform bone in model.GetComponentsInChildren<Transform>())
            bones[bone.name.Replace(BonePrefix, "")] = bone;

        Transform hipLeft = mannequin.Find(CharacterModel.HipLeft);
        Transform hipRight = mannequin.Find(CharacterModel.HipRight);
        Transform shoulderLeft = mannequin.Find(CharacterModel.ShoulderLeft);
        Transform shoulderRight = mannequin.Find(CharacterModel.ShoulderRight);

        Add(hipLeft, bones["LeftUpLeg"], bones["LeftLeg"], false);
        Add(hipLeft.Find(CharacterModel.KneeName), bones["LeftLeg"], bones["LeftFoot"], false);
        Add(hipRight, bones["RightUpLeg"], bones["RightLeg"], false);
        Add(hipRight.Find(CharacterModel.KneeName), bones["RightLeg"], bones["RightFoot"], false);
        Add(shoulderLeft, bones["LeftArm"], bones["LeftForeArm"], true);
        Add(shoulderLeft.Find(CharacterModel.ElbowName), bones["LeftForeArm"], bones["LeftHand"], true);
        Add(shoulderRight, bones["RightArm"], bones["RightForeArm"], true);
        Add(shoulderRight.Find(CharacterModel.ElbowName), bones["RightForeArm"], bones["RightHand"], true);
        Add(mannequin.Find(ProceduralModels.MannequinHeadName), bones["Head"], null, false);

        Quaternion toBody = Quaternion.Inverse(mannequin.rotation);
        for (int i = 0; i < joints.Count; i++)
        {
            Joint joint = joints[i];
            joint.Rest = toBody * joint.Rest;
            joints[i] = joint;
        }
    }

    // Руки в файле разведены в стороны: в покое они опускаются вдоль тела, как и шарниры фигуры.
    void Add(Transform pivot, Transform bone, Transform next, bool hangDown)
    {
        Quaternion rest = bone.rotation;
        if (hangDown)
            rest = Quaternion.FromToRotation(next.position - bone.position, Vector3.down) * rest;
        joints.Add(new Joint { Pivot = pivot, Bone = bone, Rest = rest });
    }

    void LateUpdate()
    {
        foreach (Joint joint in joints)
            joint.Bone.rotation = joint.Pivot.rotation * joint.Rest;
    }
}
