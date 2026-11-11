using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Grafico UI con textura sobre un poligono SIMPLE de 3 a 8 vertices.
/// Compatible con el nombre del componente anterior para evitar reemplazarlo
/// en la escena. Admite poligonos concavos mediante triangulacion de orejas.
/// Las posiciones y UV se expresan en coordenadas normalizadas 0..1.
/// </summary>
public class MappingQuadGraphic : MaskableGraphic
{
    [SerializeField, Range(1, 16)]
    private int subdivisiones = 8;

    private Texture contenido;
    private readonly List<Vector2> puntos = new List<Vector2>();
    private readonly List<Vector2> uv = new List<Vector2>();

    public override Texture mainTexture => contenido != null ? contenido : Texture2D.whiteTexture;

    public void EstablecerContenido(Texture textura)
    {
        contenido = textura;
        SetMaterialDirty();
        SetVerticesDirty();
    }

    public void EstablecerPoligono(IList<Vector2> vertices, IList<Vector2> coordenadasUV)
    {
        puntos.Clear();
        uv.Clear();
        if (vertices != null && coordenadasUV != null && vertices.Count == coordenadasUV.Count)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                puntos.Add(vertices[i]);
                uv.Add(coordenadasUV[i]);
            }
        }
        SetVerticesDirty();
    }

    // Conservado por compatibilidad con el editor anterior.
    public void EstablecerEsquinas(Vector2 bl, Vector2 br, Vector2 tr, Vector2 tl)
    {
        EstablecerPoligono(
            new[] { bl, br, tr, tl },
            new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (puntos.Count < 3 || puntos.Count != uv.Count) return;

        var triangulos = new List<int>();
        if (!MappingPolygonUtil.Triangular(puntos, triangulos)) return;

        Rect r = rectTransform.rect;
        Color32 tinte = color;
        int nivel = Mathf.Clamp(subdivisiones, 1, 16);

        if (puntos.Count == 4)
        {
            AgregarPlanoSubdividido(vh, r, tinte, nivel);
            return;
        }

        for (int i = 0; i < triangulos.Count; i += 3)
        {
            int indiceA = triangulos[i];
            int indiceB = triangulos[i + 1];
            int indiceC = triangulos[i + 2];
            int inicio = vh.currentVertCount;
            var indices = new int[(nivel + 1) * (nivel + 2) / 2];

            for (int fila = 0; fila <= nivel; fila++)
            {
                for (int columna = 0; columna <= nivel - fila; columna++)
                {
                    float b = (float)fila / nivel;
                    float c = (float)columna / nivel;
                    float a = 1f - b - c;
                    Vector2 punto = puntos[indiceA] * a + puntos[indiceB] * b + puntos[indiceC] * c;
                    Vector2 coordenadasUV = uv[indiceA] * a + uv[indiceB] * b + uv[indiceC] * c;
                    int indiceLocal = IndiceSubdividido(fila, columna, nivel);

                    indices[indiceLocal] = vh.currentVertCount;
                    vh.AddVert(
                        new Vector3(r.xMin + punto.x * r.width, r.yMin + punto.y * r.height, 0),
                        tinte,
                        coordenadasUV
                    );
                }
            }

            for (int fila = 0; fila < nivel; fila++)
            {
                for (int columna = 0; columna < nivel - fila; columna++)
                {
                    int actual = indices[IndiceSubdividido(fila, columna, nivel)];
                    int siguienteColumna = indices[IndiceSubdividido(fila, columna + 1, nivel)];
                    int siguienteFila = indices[IndiceSubdividido(fila + 1, columna, nivel)];
                    vh.AddTriangle(actual, siguienteFila, siguienteColumna);

                    if (columna < nivel - fila - 1)
                    {
                        int diagonal = indices[IndiceSubdividido(fila + 1, columna + 1, nivel)];
                        vh.AddTriangle(siguienteColumna, siguienteFila, diagonal);
                    }
                }
            }
        }
    }

    private void AgregarPlanoSubdividido(VertexHelper vh, Rect r, Color32 tinte, int nivel)
    {
        int lado = nivel + 1;
        int inicio = vh.currentVertCount;
        bool sentidoPositivo = Cruz(puntos[0], puntos[1], puntos[3]) >= 0f;

        for (int fila = 0; fila <= nivel; fila++)
        {
            float v = (float)fila / nivel;
            for (int columna = 0; columna <= nivel; columna++)
            {
                float u = (float)columna / nivel;
                float abajo = 1f - v;
                Vector2 punto = puntos[0] * (1f - u) * abajo
                    + puntos[1] * u * abajo
                    + puntos[2] * u * v
                    + puntos[3] * (1f - u) * v;
                Vector2 coordenadasUV = uv[0] * (1f - u) * abajo
                    + uv[1] * u * abajo
                    + uv[2] * u * v
                    + uv[3] * (1f - u) * v;

                vh.AddVert(
                    new Vector3(r.xMin + punto.x * r.width, r.yMin + punto.y * r.height, 0),
                    tinte,
                    coordenadasUV
                );
            }
        }

        for (int fila = 0; fila < nivel; fila++)
        {
            for (int columna = 0; columna < nivel; columna++)
            {
                int actual = inicio + fila * lado + columna;
                int derecha = actual + 1;
                int arriba = actual + lado;
                int diagonal = arriba + 1;

                if (sentidoPositivo)
                {
                    vh.AddTriangle(actual, derecha, arriba);
                    vh.AddTriangle(derecha, diagonal, arriba);
                }
                else
                {
                    vh.AddTriangle(actual, arriba, derecha);
                    vh.AddTriangle(derecha, arriba, diagonal);
                }
            }
        }
    }

    private static float Cruz(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 ab = b - a;
        Vector2 ac = c - a;
        return ab.x * ac.y - ab.y * ac.x;
    }

    private static int IndiceSubdividido(int fila, int columna, int nivel)
    {
        int desplazamiento = fila * (nivel + 1) - fila * (fila - 1) / 2;
        return desplazamiento + columna;
    }
}

/// <summary>
/// Validacion y triangulacion sin paquetes externos.
/// Solo poligonos simples: NO admite huecos ni bordes que se crucen.
/// </summary>
internal static class MappingPolygonUtil
{
    private const float Eps = 0.000001f;

    private static float Cruz(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 ab = b - a;
        Vector2 ac = c - a;
        return ab.x * ac.y - ab.y * ac.x;
    }

    private static float AreaFirmada(IList<Vector2> p)
    {
        float suma = 0f;
        for (int i = 0; i < p.Count; i++)
        {
            Vector2 a = p[i], b = p[(i + 1) % p.Count];
            suma += a.x * b.y - b.x * a.y;
        }
        return suma * 0.5f;
    }

    private static bool EnSegmento(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x >= Mathf.Min(a.x, b.x) - Eps && p.x <= Mathf.Max(a.x, b.x) + Eps &&
               p.y >= Mathf.Min(a.y, b.y) - Eps && p.y <= Mathf.Max(a.y, b.y) + Eps;
    }

    private static bool SegmentosCruzan(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float x1 = Cruz(a, b, c), x2 = Cruz(a, b, d);
        float x3 = Cruz(c, d, a), x4 = Cruz(c, d, b);

        if (((x1 > Eps && x2 < -Eps) || (x1 < -Eps && x2 > Eps)) &&
            ((x3 > Eps && x4 < -Eps) || (x3 < -Eps && x4 > Eps))) return true;

        if (Mathf.Abs(x1) <= Eps && EnSegmento(a, b, c)) return true;
        if (Mathf.Abs(x2) <= Eps && EnSegmento(a, b, d)) return true;
        if (Mathf.Abs(x3) <= Eps && EnSegmento(c, d, a)) return true;
        if (Mathf.Abs(x4) <= Eps && EnSegmento(c, d, b)) return true;
        return false;
    }

    internal static bool EsValido(IList<Vector2> p)
    {
        if (p == null || p.Count < 3 || p.Count > 8) return false;
        if (Mathf.Abs(AreaFirmada(p)) < 0.00001f) return false;
        for (int i = 0; i < p.Count; i++)
        {
            if ((p[i] - p[(i + 1) % p.Count]).sqrMagnitude < 0.0000001f)
                return false;
        }
        for (int i = 0; i < p.Count; i++)
        {
            int inext = (i + 1) % p.Count;
            for (int j = i + 1; j < p.Count; j++)
            {
                int jnext = (j + 1) % p.Count;
                // Los lados consecutivos comparten un vertice por diseno.
                if (i == j || inext == j || jnext == i) continue;
                if (SegmentosCruzan(p[i], p[inext], p[j], p[jnext])) return false;
            }
        }
        return true;
    }

    private static bool PuntoEnTriangulo(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float sentido)
    {
        return sentido * Cruz(a, b, p) >= -Eps &&
               sentido * Cruz(b, c, p) >= -Eps &&
               sentido * Cruz(c, a, p) >= -Eps;
    }

    internal static bool Triangular(IList<Vector2> p, List<int> indices)
    {
        indices.Clear();
        if (!EsValido(p)) return false;

        var restantes = new List<int>();
        for (int i = 0; i < p.Count; i++) restantes.Add(i);
        float sentido = Mathf.Sign(AreaFirmada(p));

        // Para 8 vertices se necesitan como maximo 5 cortes + triangulo final.
        int seguridad = 0;
        while (restantes.Count > 3 && seguridad++ < 20)
        {
            bool encontrado = false;
            for (int k = 0; k < restantes.Count; k++)
            {
                int a = restantes[(k - 1 + restantes.Count) % restantes.Count];
                int b = restantes[k];
                int c = restantes[(k + 1) % restantes.Count];
                if (sentido * Cruz(p[a], p[b], p[c]) <= Eps) continue;

                bool contiene = false;
                foreach (int q in restantes)
                {
                    if (q == a || q == b || q == c) continue;
                    if (PuntoEnTriangulo(p[q], p[a], p[b], p[c], sentido))
                    {
                        contiene = true;
                        break;
                    }
                }
                if (contiene) continue;

                if (sentido > 0f)
                {
                    indices.Add(a); indices.Add(b); indices.Add(c);
                }
                else
                {
                    indices.Add(a); indices.Add(c); indices.Add(b);
                }
                restantes.RemoveAt(k);
                encontrado = true;
                break;
            }
            if (!encontrado) { indices.Clear(); return false; }
        }

        if (restantes.Count != 3 ||
            sentido * Cruz(p[restantes[0]], p[restantes[1]], p[restantes[2]]) <= Eps)
        {
            indices.Clear();
            return false;
        }
        if (sentido > 0f)
        {
            indices.Add(restantes[0]); indices.Add(restantes[1]); indices.Add(restantes[2]);
        }
        else
        {
            indices.Add(restantes[0]); indices.Add(restantes[2]); indices.Add(restantes[1]);
        }
        return true;
    }
}
