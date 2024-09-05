using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class TilemapPixelCollider2D : MonoBehaviour
{
    public enum RegenerationMode { Manual, Automatic, Continuous }
    [Range(0, 1)]
    public float alphaCutoff = 0.5f;
    public RegenerationMode regenerationMode = RegenerationMode.Automatic;
    public void Start()
    {
        if (regenerationMode == RegenerationMode.Continuous || regenerationMode == RegenerationMode.Automatic)
        {
            Regenerate();
        }
    }
    private void Update()
    {
        if (regenerationMode == RegenerationMode.Continuous)
        {
            Regenerate();
        }
        else if (regenerationMode == RegenerationMode.Automatic)
        {

        }
    }
    public void Regenerate()
    {
        alphaCutoff = Mathf.Clamp(alphaCutoff, 0, 1);
        PolygonCollider2D PGC2D = GetComponent<PolygonCollider2D>();
        if (PGC2D == null)
        {
            throw new Exception($"PixelCollider2D could not be regenerated because there is no PolygonCollider2D component on \"{gameObject.name}\".");
        }
        SpriteRenderer SR = GetComponent<SpriteRenderer>();
        if (SR == null)
        {
            PGC2D.pathCount = 0;
            throw new Exception($"PixelCollider2D could not be regenerated because there is no SpriteRenderer component on \"{gameObject.name}\".");
        }
        if (SR.sprite == null)
        {
            PGC2D.pathCount = 0;
            return;
        }
        if (SR.sprite.texture == null)
        {
            PGC2D.pathCount = 0;
            return;
        }
        if (SR.sprite.texture.isReadable == false)
        {
            PGC2D.pathCount = 0;
            throw new Exception($"PixelCollider2D could not be regenerated because on \"{gameObject.name}\" because the sprite does not allow read/write operations.");
        }
        List<List<Vector2Int>> Pixel_Paths = new List<List<Vector2Int>>();
        Pixel_Paths = Get_Unit_Paths(SR.sprite.texture, alphaCutoff);
        Pixel_Paths = Simplify_Paths_Phase_1(Pixel_Paths);
        Pixel_Paths = Simplify_Paths_Phase_2(Pixel_Paths);
        List<List<Vector2>> World_Paths = new List<List<Vector2>>();
        World_Paths = Finalize_Paths(Pixel_Paths, SR.sprite);
        PGC2D.pathCount = World_Paths.Count;
        for (int p = 0; p < World_Paths.Count; p++)
        {
            PGC2D.SetPath(p, World_Paths[p].ToArray());
        }
    }
    private List<List<Vector2>> Finalize_Paths(List<List<Vector2Int>> Pixel_Paths, Sprite sprite)
    {
        Vector2 pivot = sprite.pivot;
        pivot.x *= Mathf.Abs(sprite.bounds.max.x - sprite.bounds.min.x);
        pivot.x /= sprite.texture.width;
        pivot.y *= Mathf.Abs(sprite.bounds.max.y - sprite.bounds.min.y);
        pivot.y /= sprite.texture.height;

        List<List<Vector2>> Output = new List<List<Vector2>>();
        for (int p = 0; p < Pixel_Paths.Count; p++)
        {
            List<Vector2> Current_List = new List<Vector2>();
            for (int o = 0; o < Pixel_Paths[p].Count; o++)
            {
                Vector2 point = Pixel_Paths[p][o];
                point.x *= Mathf.Abs(sprite.bounds.max.x - sprite.bounds.min.x);
                point.x /= sprite.texture.width;
                point.y *= Mathf.Abs(sprite.bounds.max.y - sprite.bounds.min.y);
                point.y /= sprite.texture.height;
                point -= pivot;
                Current_List.Add(point);
            }
            Output.Add(Current_List);
        }
        return Output;
    }
    private static List<List<Vector2Int>> Simplify_Paths_Phase_1(List<List<Vector2Int>> Unit_Paths)
    {
        List<List<Vector2Int>> Output = new List<List<Vector2Int>>();
        while (Unit_Paths.Count > 0)
        {
            List<Vector2Int> Current_Path = new List<Vector2Int>(Unit_Paths[0]);
            Unit_Paths.RemoveAt(0);
            bool Keep_Looping = true;
            while (Keep_Looping)
            {
                Keep_Looping = false;
                for (int p = 0; p < Unit_Paths.Count; p++)
                {
                    if (Current_Path[Current_Path.Count - 1] == Unit_Paths[p][0])
                    {
                        Keep_Looping = true;
                        Current_Path.RemoveAt(Current_Path.Count - 1);
                        Current_Path.AddRange(Unit_Paths[p]);
                        Unit_Paths.RemoveAt(p);
                        p--;
                    }
                    else if (Current_Path[0] == Unit_Paths[p][Unit_Paths[p].Count - 1])
                    {
                        Keep_Looping = true;
                        Current_Path.RemoveAt(0);
                        Current_Path.InsertRange(0, Unit_Paths[p]);
                        Unit_Paths.RemoveAt(p);
                        p--;
                    }
                    else
                    {
                        List<Vector2Int> Flipped_Path = new List<Vector2Int>(Unit_Paths[p]);
                        Flipped_Path.Reverse();
                        if (Current_Path[Current_Path.Count - 1] == Flipped_Path[0])
                        {
                            Keep_Looping = true;
                            Current_Path.RemoveAt(Current_Path.Count - 1);
                            Current_Path.AddRange(Flipped_Path);
                            Unit_Paths.RemoveAt(p);
                            p--;
                        }
                        else if (Current_Path[0] == Flipped_Path[Flipped_Path.Count - 1])
                        {
                            Keep_Looping = true;
                            Current_Path.RemoveAt(0);
                            Current_Path.InsertRange(0, Flipped_Path);
                            Unit_Paths.RemoveAt(p);
                            p--;
                        }
                    }
                }
            }
            Output.Add(Current_Path);
        }
        return Output;
    }
    private static List<List<Vector2Int>> Simplify_Paths_Phase_2(List<List<Vector2Int>> Input_Paths)
    {
        for (int pa = 0; pa < Input_Paths.Count; pa++)
        {
            for (int po = 0; po < Input_Paths[pa].Count; po++)
            {
                Vector2Int Start = new Vector2Int();
                if (po == 0)
                {
                    Start = Input_Paths[pa][Input_Paths[pa].Count - 1];
                }
                else
                {
                    Start = Input_Paths[pa][po - 1];
                }
                Vector2Int End = new Vector2Int();
                if (po == Input_Paths[pa].Count - 1)
                {
                    End = Input_Paths[pa][0];
                }
                else
                {
                    End = Input_Paths[pa][po + 1];
                }
                Vector2Int Current_Point = Input_Paths[pa][po];
                Vector2 Direction1 = Current_Point - (Vector2)Start;
                Direction1 /= Direction1.magnitude;
                Vector2 Direction2 = End - (Vector2)Start;
                Direction2 /= Direction2.magnitude;
                if (Direction1 == Direction2)
                {
                    Input_Paths[pa].RemoveAt(po);
                    po--;
                }
            }
        }
        return Input_Paths;
    }
    private static List<List<Vector2Int>> Get_Unit_Paths(Texture2D texture, float alphaCutoff)
    {
        List<List<Vector2Int>> Output = new List<List<Vector2Int>>();
        for (int x = 0; x < texture.width; x++)
        {
            for (int y = 0; y < texture.height; y++)
            {
                if (pixelSolid(texture, new Vector2Int(x, y), alphaCutoff))
                {
                    if (!pixelSolid(texture, new Vector2Int(x, y + 1), alphaCutoff))
                    {
                        Output.Add(new List<Vector2Int>() { new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1) });
                    }
                    if (!pixelSolid(texture, new Vector2Int(x, y - 1), alphaCutoff))
                    {
                        Output.Add(new List<Vector2Int>() { new Vector2Int(x, y), new Vector2Int(x + 1, y) });
                    }
                    if (!pixelSolid(texture, new Vector2Int(x + 1, y), alphaCutoff))
                    {
                        Output.Add(new List<Vector2Int>() { new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1) });
                    }
                    if (!pixelSolid(texture, new Vector2Int(x - 1, y), alphaCutoff))
                    {
                        Output.Add(new List<Vector2Int>() { new Vector2Int(x, y), new Vector2Int(x, y + 1) });
                    }
                }
            }
        }
        return Output;
    }
    private static bool pixelSolid(Texture2D texture, Vector2Int point, float alphaCutoff)
    {
        if (point.x < 0 || point.y < 0 || point.x >= texture.width || point.y >= texture.height)
        {
            return false;
        }
        float pixelAlpha = texture.GetPixel(point.x, point.y).a;
        if (alphaCutoff == 0)
        {
            if (pixelAlpha != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else if (alphaCutoff == 1)
        {
            if (pixelAlpha == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return pixelAlpha >= alphaCutoff;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TilemapPixelCollider2D))]
public class TilemapPixelColider2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        TilemapPixelCollider2D PC2D = (TilemapPixelCollider2D)target;
        if (GUILayout.Button("Regenerate Collider"))
        {
            PC2D.Regenerate();
        }
    }
}
#endif