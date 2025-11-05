using UnityEngine;

[ExecuteAlways]
public class GridGizmo : MonoBehaviour
{
    public Vector2Int size = new Vector2Int(20, 20);
    public float cellSize = 1f;
    public Color color = new Color(0f, 1f, 0f, 0.35f);
    public float yOffset = 0.05f;
    public bool drawInGame = true;

    private Material lineMaterial;

    void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            // Enable transparency
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }

    void OnDrawGizmos()
    {
        // Always draw in Scene view
        DrawGridGizmos();
    }

    void OnRenderObject()
    {
        // Also draw in Game view (runtime)
        if (!drawInGame || !Application.isPlaying)
            return;

        CreateLineMaterial();
        lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);
        GL.Begin(GL.LINES);
        GL.Color(color);

        Vector3 origin = new Vector3(
            -size.x * 0.5f * cellSize,
            yOffset,
            -size.y * 0.5f * cellSize
        );

        for (int x = 0; x <= size.x; x++)
        {
            GL.Vertex(origin + new Vector3(x * cellSize, 0, 0));
            GL.Vertex(origin + new Vector3(x * cellSize, 0, size.y * cellSize));
        }

        for (int y = 0; y <= size.y; y++)
        {
            GL.Vertex(origin + new Vector3(0, 0, y * cellSize));
            GL.Vertex(origin + new Vector3(size.x * cellSize, 0, y * cellSize));
        }

        GL.End();
        GL.PopMatrix();
    }

    private void DrawGridGizmos()
    {
        Gizmos.color = color;
        Vector3 origin = new Vector3(
            transform.position.x - size.x * 0.5f * cellSize,
            transform.position.y + yOffset,
            transform.position.z - size.y * 0.5f * cellSize
        );

        for (int x = 0; x <= size.x; x++)
        {
            Vector3 a = origin + new Vector3(x * cellSize, 0, 0);
            Vector3 b = a + new Vector3(0, 0, size.y * cellSize);
            Gizmos.DrawLine(a, b);
        }

        for (int y = 0; y <= size.y; y++)
        {
            Vector3 a = origin + new Vector3(0, 0, y * cellSize);
            Vector3 b = a + new Vector3(size.x * cellSize, 0, 0);
            Gizmos.DrawLine(a, b);
        }
    }
}
