using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor CamPlus+ P0.4: multiples superficies, de 3 a 8 vertices.
/// N: nueva superficie; TAB / Shift+TAB: seleccionar superficie.
/// A o boton: activar insercion; click: colocar vertice en el contorno mas cercano.
/// X o boton: borrar vertice seleccionado; SUPR: borrar superficie completa.
/// Mouse: arrastrar punto; flechas: 1px; Shift+flechas: 10px.
/// ENTER: guardar JSON; V: visibilidad; F1: ocultar editor; ESC: cancelar insercion.
/// Independiente de Firebase y ProyeccionController.
/// </summary>
public class MultiSurfaceMappingController : MonoBehaviour
{
    [Header("Editor")]
    [SerializeField] private int tamanoControl = 30;
    [SerializeField] private float radioSeleccionPixeles = 35f;
    [SerializeField] private bool mostrarControlesAlIniciar = true;
    [SerializeField, Range(0.65f, 2f)] private float escalaPanel = 1f;

    [Header("Archivo de calibracion")]
    [SerializeField] private string archivoJSON = "camplus_mapping_calibracion.json";

    [Serializable]
    public class DatosSuperficie
    {
        public string nombre;
        public bool visible = true;
        public List<Vector2> vertices = new List<Vector2>();
        public List<Vector2> uvs = new List<Vector2>();
        public bool uvsPorCaja = false;
        // Compatibilidad: formato P0.3 (4 esquinas).
        public Vector2 bl, br, tr, tl;
    }

    [Serializable]
    private class ArchivoSuperficies
    {
        public int version = 2;
        public List<DatosSuperficie> superficies = new List<DatosSuperficie>();
    }

    private class SuperficieRuntime
    {
        public DatosSuperficie datos;
        public MappingQuadGraphic dibujo;
        public readonly List<RectTransform> controles = new List<RectTransform>();
        public readonly List<Image> imagenes = new List<Image>();
        public Texture2D texturaPrueba;
        public Color colorBorde;
    }

    private readonly List<SuperficieRuntime> superficies = new List<SuperficieRuntime>();
    private readonly List<string> mensajes = new List<string>();
    private Canvas canvas;
    private RectTransform raiz;
    private int superficieActual = -1;
    private int verticeActual;
    private bool arrastrando;
    private bool modoAnadir;
    private bool mostrarControles;
    private bool cambiosSinGuardar;
    private bool avisoFiguraInvalida;
    private GUIStyle estiloTitulo;
    private GUIStyle estiloTexto;
    private int anchoPrevio, altoPrevio;

    private const float PanelX = 10f, PanelY = 10f;
    private const float PanelAncho = 650f, PanelAlto = 470f;
    private string RutaArchivo => Path.Combine(Application.persistentDataPath, archivoJSON);
    private bool mostrarPanel = true;
    private bool edicionActiva = true;

    private const string ClaveEscalaPanel =
        "CamPlusMapping_EscalaPanel";

    private void Start()
    {
        mostrarControles = mostrarControlesAlIniciar;
        mostrarPanel = true;

        escalaPanel = Mathf.Clamp(
            PlayerPrefs.GetFloat(
                ClaveEscalaPanel,
                escalaPanel
            ),
            0.65f,
            2f
        );
        CrearCanvas();
        if (!Cargar()) CrearSuperficie();
        if (superficies.Count > 0) Seleccionar(0, 0);
        ActualizarAspecto();
        Registrar("Editor de poligonos 3-8 vertices iniciado.");
        Debug.Log("CamPlus+ Mapping | Archivo: " + RutaArchivo);
        anchoPrevio = Screen.width;
        altoPrevio = Screen.height;
    }

    public void EstablecerEdicion(bool activa)
    {
        edicionActiva = activa;
        mostrarPanel = activa;
        mostrarControles = activa;

        if (!activa)
        {
            CancelarInsercion();
            arrastrando = false;
        }

        ActualizarAspecto();
    }

    public List<string> ObtenerNombresSuperficies()
    {
        var nombres = new List<string>();

        foreach (var s in superficies)
            nombres.Add(s.datos.nombre);

        return nombres;
    }

    private void Update()
    {
        if (!edicionActiva)
        {
            if (anchoPrevio != Screen.width || altoPrevio != Screen.height)
            {
                anchoPrevio = Screen.width;
                altoPrevio = Screen.height;
                ActualizarAspecto();
            }

            return;
        }

        // F1 oculta solo el panel informativo.
        // Los vertices siguen disponibles para editar.
        if (Input.GetKeyDown(KeyCode.F1))
        {
            mostrarPanel = !mostrarPanel;
        }

        // F2 permite ocultar los controles para ver
        // la proyeccion sin elementos de calibracion.
        if (Input.GetKeyDown(KeyCode.F2))
        {
            mostrarControles = !mostrarControles;

            if (!mostrarControles)
                CancelarInsercion();

            ActualizarAspecto();
        }

        if (Input.GetKeyDown(KeyCode.N)) CrearSuperficie();
        if (Input.GetKeyDown(KeyCode.Tab) && superficies.Count > 0)
        {
            int paso = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
            Seleccionar((superficieActual + paso + superficies.Count) % superficies.Count, 0);
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Guardar();
        if (Input.GetKeyDown(KeyCode.Delete)) EliminarSuperficieSeleccionada();
        if (Input.GetKeyDown(KeyCode.V)) AlternarVisibilidad();
        if (Input.GetKeyDown(KeyCode.Escape)) CancelarInsercion();

        if (mostrarControles)
        {
            if (Input.GetKeyDown(KeyCode.A)) AlternarInsercion();
            if (Input.GetKeyDown(KeyCode.X)) EliminarVerticeSeleccionado();
            ProcesarMouse();
            if (!modoAnadir) ProcesarFlechas();
        }
        else arrastrando = false;

        ActualizarDestello();
        if (anchoPrevio != Screen.width || altoPrevio != Screen.height)
        {
            anchoPrevio = Screen.width;
            altoPrevio = Screen.height;
            ActualizarAspecto();
        }
    }

    private void CrearCanvas()
    {
        GameObject go = new GameObject("Mapping_Overlay_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        raiz = go.GetComponent<RectTransform>();
    }

    private void CrearSuperficie()
    {
        CancelarInsercion();
        int numero = 1;
        while (superficies.Exists(s => s.datos.nombre == "Superficie_" + numero.ToString("00"))) numero++;
        float d = 0.045f * (superficies.Count % 5);
        var datos = new DatosSuperficie
        {
            nombre = "Superficie_" + numero.ToString("00"),
            visible = true,
            vertices = new List<Vector2>
            {
                new Vector2(0.24f + d, 0.30f + d),
                new Vector2(0.49f + d, 0.30f + d),
                new Vector2(0.49f + d, 0.64f + d),
                new Vector2(0.24f + d, 0.64f + d)
            },
            uvs = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }
        };
        CrearDesdeDatos(datos);
        Seleccionar(superficies.Count - 1, 0);
        cambiosSinGuardar = true;
        Registrar("Nueva superficie: " + datos.nombre);
    }

    private static bool NormalizarDatos(DatosSuperficie datos)
    {
        if (datos == null) return false;
        // Migrar calibracion P0.3 sin perder sus cuatro puntos.
        if (datos.vertices == null || datos.vertices.Count == 0)
        {
            datos.vertices = new List<Vector2> { datos.bl, datos.br, datos.tr, datos.tl };
            datos.uvs = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        }
        if (datos.uvs == null || datos.uvs.Count != datos.vertices.Count)
        {
            // Recuperacion de un archivo parcial; UV estimadas por caja envolvente.
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            foreach (Vector2 p in datos.vertices)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            datos.uvs = new List<Vector2>();
            foreach (Vector2 p in datos.vertices)
                datos.uvs.Add(new Vector2((p.x - minX) / Mathf.Max(0.001f, maxX - minX),
                    (p.y - minY) / Mathf.Max(0.001f, maxY - minY)));
        }
        return MappingPolygonUtil.EsValido(datos.vertices);
    }

    private void CrearDesdeDatos(DatosSuperficie datos)
    {
        var s = new SuperficieRuntime { datos = datos };
        // Corregir las superficies irregulares cargadas desde P0.4.
        if (datos.vertices.Count != 4)
            datos.uvsPorCaja = true;

        if (datos.uvsPorCaja)
            RecalcularUVs(s);
        int indice = superficies.Count;
        Color[] colores =
        {
            new Color(0.2f, 0.85f, 1f), new Color(1f, 0.35f, 0.65f),
            new Color(1f, 0.84f, 0.25f), new Color(0.35f, 1f, 0.55f),
            new Color(0.75f, 0.45f, 1f)
        };
        s.colorBorde = colores[indice % colores.Length];
        s.texturaPrueba = CrearTexturaCuadricula(s.colorBorde);

        GameObject go = new GameObject(datos.nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(MappingQuadGraphic));
        go.transform.SetParent(raiz, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        s.dibujo = go.GetComponent<MappingQuadGraphic>();
        s.dibujo.raycastTarget = false;
        s.dibujo.EstablecerContenido(s.texturaPrueba);
        s.dibujo.EstablecerPoligono(datos.vertices, datos.uvs);

        for (int j = 0; j < datos.vertices.Count; j++)
            CrearControl(s, j);

        superficies.Add(s);
        SincronizarSuperficie(s);
        OrdenarDibujo();
    }

    private void CrearControl(SuperficieRuntime s, int indice)
    {
        GameObject go = new GameObject("Vertice_" + (indice + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(raiz, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = Vector2.one * tamanoControl;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        s.controles.Insert(indice, r);
        s.imagenes.Insert(indice, img);
    }

    private Texture2D CrearTexturaCuadricula(Color color)
    {
        const int tam = 256;
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false)
        {
            name = "Mapping_Grid_Runtime",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color fondo = new Color(0.015f, 0.015f, 0.02f, 1f);
        var pixeles = new Color[tam * tam];
        for (int y = 0; y < tam; y++)
            for (int x = 0; x < tam; x++)
            {
                bool borde = x < 5 || x >= tam - 5 || y < 5 || y >= tam - 5;
                bool rejilla = x % 32 < 2 || y % 32 < 2;
                pixeles[y * tam + x] = borde ? color : (rejilla ? color * 0.70f : fondo);
            }
        tex.SetPixels(pixeles);
        tex.Apply();
        return tex;
    }

    private void OrdenarDibujo()
    {
        // Las superficies deben ir detras de TODOS los puntos manipulables.
        foreach (var s in superficies) s.dibujo.transform.SetAsLastSibling();
        foreach (var s in superficies)
            foreach (var r in s.controles) r.SetAsLastSibling();
    }

    private void DibujarContornos()
    {
        Matrix4x4 matrizOriginal = GUI.matrix;
        Color colorOriginal = GUI.color;

        GUI.matrix = Matrix4x4.identity;

        foreach (var s in superficies)
        {
            if (!s.datos.visible)
                continue;

            var vertices = s.datos.vertices;

            GUI.color = s.colorBorde;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 inicio = vertices[i];
                Vector2 fin = vertices[(i + 1) % vertices.Count];

                // Convertir coordenadas normalizadas a pantalla GUI.
                Vector2 a = new Vector2(
                    inicio.x * Screen.width,
                    (1f - inicio.y) * Screen.height
                );

                Vector2 b = new Vector2(
                    fin.x * Screen.width,
                    (1f - fin.y) * Screen.height
                );

                Vector2 delta = b - a;
                float largo = delta.magnitude;

                if (largo < 0.1f)
                    continue;

                Vector2 centro = (a + b) * 0.5f;
                float angulo = Mathf.Atan2(
                    delta.y,
                    delta.x
                ) * Mathf.Rad2Deg;

                GUI.matrix = Matrix4x4.identity;
                GUIUtility.RotateAroundPivot(angulo, centro);

                GUI.DrawTexture(
                    new Rect(
                        centro.x - largo * 0.5f,
                        centro.y - 2f,
                        largo,
                        4f
                    ),
                    Texture2D.whiteTexture
                );
            }
        }

        GUI.matrix = matrizOriginal;
        GUI.color = colorOriginal;
    }

    private void SincronizarSuperficie(SuperficieRuntime s)
    {
        s.dibujo.gameObject.SetActive(s.datos.visible);
        s.dibujo.EstablecerPoligono(s.datos.vertices, s.datos.uvs);
        bool controlesVisibles = mostrarControles && superficieActual >= 0 && superficies[superficieActual] == s;
        for (int j = 0; j < s.controles.Count; j++)
        {
            s.controles[j].gameObject.SetActive(controlesVisibles);
            Vector2 p = s.datos.vertices[j];
            s.controles[j].anchoredPosition = new Vector2(p.x * Screen.width, p.y * Screen.height);
            s.imagenes[j].color = j == verticeActual ? new Color(1f, 0.25f, 0.25f) : Color.white;
        }
    }

    private void ActualizarAspecto()
    {
        foreach (var s in superficies) SincronizarSuperficie(s);
        OrdenarDibujo();
    }

    private void Seleccionar(int indice, int vertice)
    {
        if (indice < 0 || indice >= superficies.Count) return;
        if (indice != superficieActual) CancelarInsercion();
        superficieActual = indice;
        verticeActual = Mathf.Clamp(vertice, 0, superficies[indice].datos.vertices.Count - 1);
        ActualizarAspecto();
        Registrar("Seleccionada: " + superficies[indice].datos.nombre);
    }

    private void AlternarInsercion()
    {
        if (modoAnadir) { CancelarInsercion(); return; }
        if (superficieActual < 0) { Registrar("Primero crea o selecciona una superficie."); return; }
        var s = superficies[superficieActual];
        if (s.datos.vertices.Count >= 8) { Registrar("Limite alcanzado: maximo 8 vertices."); return; }
        if (!s.datos.visible)
        {
            s.datos.visible = true;
            cambiosSinGuardar = true;
            SincronizarSuperficie(s);
        }
        modoAnadir = true;
        arrastrando = false;
        Registrar("MODO ANADIR: clic en el nuevo punto. ESC para cancelar.");
    }

    private void CancelarInsercion()
    {
        if (!modoAnadir) return;
        modoAnadir = false;
        foreach (var s in superficies) if (s.dibujo != null) s.dibujo.color = Color.white;
        Registrar("Insercion cancelada.");
    }

    private void ActualizarDestello()
    {
        if (superficieActual < 0) return;
        var s = superficies[superficieActual];
        if (s.dibujo == null) return;
        float a = modoAnadir ? Mathf.Lerp(0.28f, 1f, Mathf.PingPong(Time.unscaledTime * 1.7f, 1f)) : 1f;
        Color actual = s.dibujo.color;
        if (Mathf.Abs(actual.a - a) > 0.01f)
            s.dibujo.color = new Color(1f, 1f, 1f, a);
    }

    private bool MouseSobrePanel(Vector2 mouse)
    {
        // Si el panel esta oculto, no bloquea ninguna zona.
        if (!mostrarPanel)
            return false;

        float escala = EscalaPanelVisible();

        Vector2 gui = new Vector2(
            mouse.x,
            Screen.height - mouse.y
        );

        return new Rect(
            PanelX * escala,
            PanelY * escala,
            PanelAncho * escala,
            PanelAlto * escala
        ).Contains(gui);
    }

    private float EscalaPanelVisible()
    {
        float limiteAncho =
            (Screen.width - 16f) / (PanelX + PanelAncho);

        float limiteAlto =
            (Screen.height - 16f) / (PanelY + PanelAlto);

        float limite = Mathf.Max(
            0.65f,
            Mathf.Min(2f, Mathf.Min(limiteAncho, limiteAlto))
        );

        return Mathf.Min(escalaPanel, limite);
    }

    private void AjustarEscalaPanel(float cambio)
    {
        escalaPanel = Mathf.Clamp(
            escalaPanel + cambio,
            0.65f,
            2f
        );

        PlayerPrefs.SetFloat(
            ClaveEscalaPanel,
            escalaPanel
        );

        PlayerPrefs.Save();
    }

    private static Vector2 NormalizarMouse(Vector2 p)
    {
        return new Vector2(Mathf.Clamp01(p.x / Mathf.Max(1, Screen.width)),
            Mathf.Clamp01(p.y / Mathf.Max(1, Screen.height)));
    }

    private void ProcesarMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            arrastrando = false;
            Vector2 mouse = Input.mousePosition;
            if (MouseSobrePanel(mouse)) return;
            if (modoAnadir) { AnadirEnPosicion(NormalizarMouse(mouse)); return; }

            for (int i = superficies.Count - 1; i >= 0; i--)
            {
                if (!superficies[i].datos.visible) continue;
                for (int j = 0; j < superficies[i].datos.vertices.Count; j++)
                {
                    Vector2 p = superficies[i].datos.vertices[j];
                    Vector2 pixel = new Vector2(p.x * Screen.width, p.y * Screen.height);
                    if (Vector2.Distance(mouse, pixel) > radioSeleccionPixeles) continue;
                    Seleccionar(i, j);
                    arrastrando = true;
                    break;
                }
                if (arrastrando) break;
            }
        }
        if (arrastrando && Input.GetMouseButton(0) && superficieActual >= 0)
            MoverSeleccionado(NormalizarMouse(Input.mousePosition));
        if (Input.GetMouseButtonUp(0)) { arrastrando = false; avisoFiguraInvalida = false; }
    }

    private void MoverSeleccionado(Vector2 nuevo)
    {
        var s = superficies[superficieActual];
        var vertices = s.datos.vertices;
        Vector2 previo = vertices[verticeActual];
        if ((previo - nuevo).sqrMagnitude < 0.000000001f) return;
        vertices[verticeActual] = nuevo;
        var triangulos = new List<int>();
        if (!MappingPolygonUtil.Triangular(vertices, triangulos))
        {
            vertices[verticeActual] = previo;
            if (!avisoFiguraInvalida) Registrar("Movimiento bloqueado: bordes cruzados o area invalida.");
            avisoFiguraInvalida = true;
            return;
        }
        avisoFiguraInvalida = false;
        cambiosSinGuardar = true;

        if (s.datos.uvsPorCaja)
            RecalcularUVs(s);

        SincronizarSuperficie(s);
    }

    private void ProcesarFlechas()
    {
        if (superficieActual < 0 || arrastrando) return;
        int dx = 0, dy = 0;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) dx--;
        if (Input.GetKeyDown(KeyCode.RightArrow)) dx++;
        if (Input.GetKeyDown(KeyCode.UpArrow)) dy++;
        if (Input.GetKeyDown(KeyCode.DownArrow)) dy--;
        if (dx == 0 && dy == 0) return;
        int paso = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10 : 1;
        Vector2 p = superficies[superficieActual].datos.vertices[verticeActual];
        p.x = Mathf.Clamp01(p.x + dx * paso / Mathf.Max(1f, Screen.width));
        p.y = Mathf.Clamp01(p.y + dy * paso / Mathf.Max(1f, Screen.height));
        MoverSeleccionado(p);
    }

    private static void RecalcularUVs(SuperficieRuntime s)
    {
        var vertices = s.datos.vertices;

        if (vertices.Count < 3)
            return;

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        foreach (Vector2 p in vertices)
        {
            minX = Mathf.Min(minX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxX = Mathf.Max(maxX, p.x);
            maxY = Mathf.Max(maxY, p.y);
        }

        float ancho = Mathf.Max(0.0001f, maxX - minX);
        float alto = Mathf.Max(0.0001f, maxY - minY);

        s.datos.uvs.Clear();

        foreach (Vector2 p in vertices)
        {
            s.datos.uvs.Add(new Vector2(
                (p.x - minX) / ancho,
                (p.y - minY) / alto
            ));
        }
    }
    private void AnadirEnPosicion(Vector2 punto)
    {
        if (superficieActual < 0) return;
        var s = superficies[superficieActual];
        var v = s.datos.vertices;
        var uv = s.datos.uvs;
        if (v.Count >= 8) { Registrar("Maximo 8 vertices."); CancelarInsercion(); return; }

        // Elegir el lado mas cercano. Insercion en orden conserva el contorno.
        int lado = 0;
        float tMejor = 0f;
        float dMejor = float.PositiveInfinity;
        for (int i = 0; i < v.Count; i++)
        {
            Vector2 a = v[i], b = v[(i + 1) % v.Count];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(punto - a, ab) / Mathf.Max(0.000001f, ab.sqrMagnitude));
            float d = (punto - (a + ab * t)).sqrMagnitude;
            if (d >= dMejor) continue;
            dMejor = d;
            lado = i;
            tMejor = t;
        }

        int insertar = lado + 1;
        var nuevos = new List<Vector2>(v);
        nuevos.Insert(insertar, punto);
        var triangulos = new List<int>();
        if (!MappingPolygonUtil.Triangular(nuevos, triangulos))
        {
            Registrar("Punto rechazado: cruzaria bordes o dejaria un area invalida. Prueba cerca de otro borde.");
            return;
        }

        Vector2 uvNueva = Vector2.Lerp(uv[lado], uv[(lado + 1) % uv.Count], tMejor);
        v.Insert(insertar, punto);
        uv.Insert(insertar, uvNueva);

        // Los polígonos editados utilizan UV ajustadas al contorno.
        s.datos.uvsPorCaja = true;
        RecalcularUVs(s);
        CrearControl(s, insertar);
        verticeActual = insertar;
        modoAnadir = false;
        cambiosSinGuardar = true;
        s.dibujo.color = Color.white;
        ActualizarAspecto();
        Registrar("Vertice agregado (" + v.Count + "/8) a " + s.datos.nombre + ".");
    }

    private void EliminarVerticeSeleccionado()
    {
        if (superficieActual < 0) { Registrar("No hay superficie seleccionada."); return; }
        CancelarInsercion();
        var s = superficies[superficieActual];
        if (s.datos.vertices.Count <= 3)
        {
            Registrar("No se puede: minimo 3 vertices por superficie.");
            return;
        }

        var nuevos = new List<Vector2>(s.datos.vertices);
        nuevos.RemoveAt(verticeActual);
        var triangulos = new List<int>();
        if (!MappingPolygonUtil.Triangular(nuevos, triangulos))
        {
            Registrar("No se puede quitar ese vertice: la figura resultaria invalida.");
            return;
        }
        Destroy(s.controles[verticeActual].gameObject);
        s.controles.RemoveAt(verticeActual);
        s.imagenes.RemoveAt(verticeActual);
        s.datos.vertices.RemoveAt(verticeActual);
        s.datos.uvs.RemoveAt(verticeActual);
        if (s.datos.uvsPorCaja)
            RecalcularUVs(s);
        verticeActual = Mathf.Clamp(verticeActual, 0, s.datos.vertices.Count - 1);
        cambiosSinGuardar = true;
        ActualizarAspecto();
        Registrar("Vertice eliminado. Quedan " + s.datos.vertices.Count + ".");
    }

    private void AlternarVisibilidad()
    {
        if (superficieActual < 0) return;
        CancelarInsercion();
        var s = superficies[superficieActual];
        s.datos.visible = !s.datos.visible;
        cambiosSinGuardar = true;
        SincronizarSuperficie(s);
        Registrar(s.datos.nombre + (s.datos.visible ? " visible" : " oculta"));
    }

    private void EliminarSuperficieSeleccionada()
    {
        if (superficieActual < 0) return;
        CancelarInsercion();
        var s = superficies[superficieActual];
        string nombre = s.datos.nombre;
        Destroy(s.dibujo.gameObject);
        foreach (var r in s.controles) Destroy(r.gameObject);
        if (s.texturaPrueba != null) Destroy(s.texturaPrueba);
        int indice = superficieActual;
        superficies.RemoveAt(indice);
        superficieActual = superficies.Count == 0 ? -1 : Mathf.Clamp(indice, 0, superficies.Count - 1);
        verticeActual = 0;
        cambiosSinGuardar = true;
        ActualizarAspecto();
        Registrar("Superficie eliminada: " + nombre + " (ENTER para guardar)");
    }

    private void Guardar()
    {
        foreach (var s in superficies)
        {
            var triangles = new List<int>();
            if (!MappingPolygonUtil.Triangular(s.datos.vertices, triangles))
            {
                Registrar("No guardado: poligono invalido en " + s.datos.nombre);
                return;
            }
        }
        var archivo = new ArchivoSuperficies();
        foreach (var s in superficies) archivo.superficies.Add(s.datos);
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(RutaArchivo, JsonUtility.ToJson(archivo, true));
            cambiosSinGuardar = false;
            Registrar("GUARDADO: " + superficies.Count + " superficie(s) en JSON v2.");
            Debug.Log("CamPlus+ Mapping guardado en: " + RutaArchivo);
        }
        catch (Exception ex) { Registrar("ERROR al guardar: " + ex.Message); Debug.LogException(ex); }
    }

    private bool Cargar()
    {
        if (!File.Exists(RutaArchivo)) return false;
        try
        {
            ArchivoSuperficies archivo = JsonUtility.FromJson<ArchivoSuperficies>(File.ReadAllText(RutaArchivo));
            if (archivo == null || archivo.superficies == null) return false;
            int omitidas = 0;
            foreach (var datos in archivo.superficies)
            {
                if (!NormalizarDatos(datos)) { omitidas++; continue; }
                CrearDesdeDatos(datos);
            }
            if (omitidas > 0) Registrar("Aviso: " + omitidas + " superficie(s) invalidas omitidas.");
            Registrar("Cargadas " + superficies.Count + " superficies del archivo v" + archivo.version + ".");
            cambiosSinGuardar = false;
            return true;
        }
        catch (Exception ex) { Registrar("ERROR al cargar: " + ex.Message); Debug.LogException(ex); return false; }
    }

    // Compatibilidad con la fase de imagenes / RenderTextures.
    public bool CambiarContenido(string nombre, Texture contenido)
    {
        foreach (var s in superficies)
        {
            if (s.datos.nombre != nombre) continue;
            s.dibujo.EstablecerContenido(contenido != null ? contenido : s.texturaPrueba);
            return true;
        }
        return false;
    }

    private void Registrar(string mensaje)
    {
        mensajes.Add(mensaje);
        if (mensajes.Count > 6) mensajes.RemoveAt(0);
        Debug.Log("[CamPlus Mapping] " + mensaje);
    }


    private void OnGUI()
    {
        if (!mostrarPanel && !mostrarControles)
            return;

        if (estiloTitulo == null)
        {
            estiloTitulo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            estiloTexto = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        // PANEL DE CONTROLES
        if (mostrarPanel)
        {
            Matrix4x4 anterior = GUI.matrix;
            float escala = EscalaPanelVisible();

            GUI.matrix = Matrix4x4.Scale(
                new Vector3(escala, escala, 1f)
            );

            GUI.Box(
                new Rect(PanelX, PanelY, PanelAncho, PanelAlto),
                ""
            );

            GUILayout.BeginArea(
                new Rect(
                    PanelX + 14,
                    PanelY + 10,
                    PanelAncho - 28,
                    PanelAlto - 20
                )
            );

            // Titulo y botones de escala
            GUILayout.BeginHorizontal();

            GUILayout.Label(
                "CAMPLUS+ | MAPPING P0.4.1",
                estiloTitulo,
                GUILayout.ExpandWidth(true)
            );

            if (GUILayout.Button("A-", GUILayout.Width(55),
                GUILayout.Height(30)))
            {
                AjustarEscalaPanel(-0.1f);
            }

            if (GUILayout.Button("A+", GUILayout.Width(55),
                GUILayout.Height(30)))
            {
                AjustarEscalaPanel(0.1f);
            }

            GUILayout.EndHorizontal();

            string nombre = superficieActual >= 0
                ? superficies[superficieActual].datos.nombre
                : "Ninguna";

            int total = superficieActual >= 0
                ? superficies[superficieActual].datos.vertices.Count
                : 0;

            GUILayout.Label(
                "Area: " + nombre +
                " (" + superficies.Count + " total) | Vertices: " +
                total + "/8" +
                (cambiosSinGuardar ? " * SIN GUARDAR" : " Guardado"),
                estiloTexto
            );

            GUILayout.Label(
                "N: nueva | TAB: cambiar | ENTER: guardar",
                estiloTexto
            );

            GUILayout.Label(
                "F1: panel | F2: controles | V: visibilidad",
                estiloTexto
            );

            GUILayout.Label(
                "Arrastrar: mover vertice | Flechas: 1 px",
                estiloTexto
            );

            GUILayout.Label(
                "Shift+flechas: 10 px | SUPR: borrar area | ESC: cancelar",
                estiloTexto
            );

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = modoAnadir
                ? new Color(1f, 0.8f, 0.2f)
                : Color.white;

            if (GUILayout.Button(
                modoAnadir
                    ? "CANCELAR INSERCION [A]"
                    : "ANADIR VERTICE [A]",
                GUILayout.Height(36)))
            {
                AlternarInsercion();
            }

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button(
                "ELIMINAR VERTICE [X]",
                GUILayout.Height(36)))
            {
                EliminarVerticeSeleccionado();
            }

            GUILayout.EndHorizontal();

            GUILayout.Label(
                modoAnadir
                    ? "MODO INSERCION: haz clic en el nuevo punto."
                    : "Seleccionado: vertice " +
                      (superficieActual < 0
                          ? "-"
                          : (verticeActual + 1).ToString()),
                estiloTexto
            );

            GUILayout.Space(6);

            GUILayout.Label("REGISTRO:", estiloTitulo);

            foreach (string mensaje in mensajes)
            {
                GUILayout.Label("• " + mensaje, estiloTexto);
            }

            GUILayout.EndArea();
            GUI.matrix = anterior;
        }

        // CONTORNOS Y NUMEROS INDEPENDIENTES DEL PANEL
        if (mostrarControles)
        {
            DibujarContornos();

            if (superficieActual >= 0)
            {
                var s = superficies[superficieActual];

                for (int j = 0; j < s.datos.vertices.Count; j++)
                {
                    Vector2 p = s.datos.vertices[j];

                    GUI.Label(
                        new Rect(
                            p.x * Screen.width + 14,
                            Screen.height - p.y * Screen.height - 16,
                            40,
                            25
                        ),
                        (j + 1).ToString(),
                        estiloTexto
                    );
                }
            }
        }
    }


    private void OnDestroy()
    {
        foreach (var s in superficies)
            if (s.texturaPrueba != null) Destroy(s.texturaPrueba);
    }
}