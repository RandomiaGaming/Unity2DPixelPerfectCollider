using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class PixelPerfectSpriteshape : MonoBehaviour
{

    public Sprite[] sprites;
    private void Start()
    {
        Regenerate();
    }
    public void Regenerate()
    {
        foreach (Sprite s in sprites)
        {
            List<Vector2> newpoints = new List<Vector2>();
            for (int height = 0; height < s.texture.height; height++)
            {
                for (int width = 0; width < s.texture.width; width++)
                {
                    if (s.texture.GetPixel(width, height).a > 0)
                    {
                        if (GetPixel(width + 1, height + 1, s.texture).a == 0 && GetPixel(width + 1, height, s.texture).a == GetPixel(width, height + 1, s.texture).a)
                        {
                            newpoints.Add(new Vector2(width + 1, height + 1));
                        }
                        if (GetPixel(width - 1, height - 1, s.texture).a == 0 && GetPixel(width - 1, height, s.texture).a == GetPixel(width, height - 1, s.texture).a)
                        {
                            newpoints.Add(new Vector2(width, height));
                        }
                        if (GetPixel(width - 1, height + 1, s.texture).a == 0 && GetPixel(width - 1, height, s.texture).a == GetPixel(width, height + 1, s.texture).a)
                        {
                            newpoints.Add(new Vector2(width, height + 1));
                        }
                        if (GetPixel(width + 1, height - 1, s.texture).a == 0 && GetPixel(width + 1, height, s.texture).a == GetPixel(width, height - 1, s.texture).a)
                        {
                            newpoints.Add(new Vector2(width + 1, height));
                        }
                    }
                }
            }

            

            List<Vector2> input = newpoints;
            newpoints = new List<Vector2>();
            Vector2 currentpoint = input[0];
            newpoints.Add(currentpoint);
            while (newpoints.Count != input.Count)
            {
                Vector2 best = new Vector2(float.MaxValue, float.MaxValue);
                foreach (Vector2 point in input)
                {
                    if (!Contains(newpoints.ToArray(), point) && point.y == currentpoint.y)
                    {
                        if (Mathf.Abs(currentpoint.x - point.x) < Mathf.Abs(currentpoint.x - best.x))
                        {
                            best = point;
                        }
                    }
                }
                if (best != new Vector2(float.MaxValue, float.MaxValue))
                {
                    newpoints.Add(best);
                    currentpoint = best;
                }
                best = new Vector2(float.MaxValue, float.MaxValue);
                foreach (Vector2 point in input)
                {
                    if (!Contains(newpoints.ToArray(), point) && point.x == currentpoint.x)
                    {
                        if (Mathf.Abs(currentpoint.y - point.y) < Mathf.Abs(currentpoint.y - best.y))
                        {
                            best = point;
                        }
                    }
                }
                if (best != new Vector2(float.MaxValue, float.MaxValue))
                {
                    newpoints.Add(best);
                    currentpoint = best;
                }

            }
            IList<Vector2[]> i = new List<Vector2[]>();
            i.Add(newpoints.ToArray());
            s.OverridePhysicsShape(i);
            
        }
    }
    public bool Contains(Vector2[] input, Vector2 contains)
    {
        foreach (Vector2 v in input)
        {
            if (v.x == contains.x && v.y == contains.y)
            {
                return true;
            }
        }
        return false;
    }
    public Color GetPixel(int x, int y, Texture2D texture)
    {
        if (x > texture.width - 1 || x < 0 || y < 0 || y > texture.height - 1)
        {
            return new Color(0, 0, 0, 0);
        }
        else
        {
            return (texture.GetPixel(x, y));
        }
    }
    
}


