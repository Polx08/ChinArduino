using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class MappingExperiencia : MonoBehaviour
{
    [Serializable]
    public class ContenidoSuperficie
    {
        public string nombreSuperficie = "Superficie_01";
        public Texture imagen;
        public VideoClip video;
    }

    [Header("Referencias")]
    public MultiSurfaceMappingController editor;
    public ExperienciaController experiencia;
    [Tooltip("El servidor queda apagado hasta terminar la calibracion")]
    public PhoneConnectionServer servidor;

    [Header("Siempre proyectadas desde el inicio")]
    public List<ContenidoSuperficie> fijas = new List<ContenidoSuperficie>();

    [Header("Fase 1 (mismo orden que los objetos de ExperienciaController)")]
    public List<ContenidoSuperficie> fase1 = new List<ContenidoSuperficie>();

    [Header("Fase 2 (mismo orden que los objetos de ExperienciaController)")]
    public List<ContenidoSuperficie> fase2 = new List<ContenidoSuperficie>();

    [Header("Final (se muestra durante la secuencia final)")]
    public List<ContenidoSuperficie> contenidoFinal = new List<ContenidoSuperficie>();

    [Header("Calibracion")]
    public KeyCode teclaCalibracion = KeyCode.F3;

    [Header("Debug sin proyector ni telefono")]
    public bool modoDebug = true;
    public KeyCode teclaDebugSiguiente = KeyCode.F5;
    public KeyCode teclaDebugReiniciar = KeyCode.F6;

    readonly Dictionary<string, Texture> estado = new Dictionary<string, Texture>();
    readonly Dictionary<string, VideoPlayer> reproductores = new Dictionary<string, VideoPlayer>();
    readonly Dictionary<string, RenderTexture> texturasVideo = new Dictionary<string, RenderTexture>();

    Texture2D vacio;
    bool calibrando = true;
    bool listo = false;
    bool experienciaIniciada = false;
    int pasoDebug = 0;
    GUIStyle estiloDebug;

    void Awake()
    {
        vacio = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        vacio.SetPixel(0, 0, Color.clear);
        vacio.Apply();

        if (servidor != null)
        {
            servidor.enabled = false;
        }
    }

    void OnEnable()
    {
        if (experiencia != null)
        {
            experiencia.alProyectar.AddListener(Proyectar);
            experiencia.alIniciarFinal.AddListener(IniciarFinal);
            experiencia.alReiniciar.AddListener(Reiniciar);
        }
    }

    void OnDisable()
    {
        if (experiencia != null)
        {
            experiencia.alProyectar.RemoveListener(Proyectar);
            experiencia.alIniciarFinal.RemoveListener(IniciarFinal);
            experiencia.alReiniciar.RemoveListener(Reiniciar);
        }
    }

    IEnumerator Start()
    {
        if (editor == null)
        {
            editor = FindFirstObjectByType<MultiSurfaceMappingController>();
        }

        if (editor == null)
        {
            Debug.LogError("MappingExperiencia: falta asignar el editor de mapping.");
            yield break;
        }

        yield return null;

        EstadoInicial();
        listo = true;
        Debug.Log("Mapping en calibracion. Ajusta las superficies, ENTER para guardar y " + teclaCalibracion + " para empezar.");
    }

    void Update()
    {
        if (!listo)
        {
            return;
        }

        if (Input.GetKeyDown(teclaCalibracion))
        {
            if (calibrando)
            {
                TerminarCalibracion();
            }
            else
            {
                EntrarCalibracion();
            }
        }

        if (!modoDebug || calibrando)
        {
            return;
        }

        if (Input.GetKeyDown(teclaDebugSiguiente))
        {
            DebugSiguiente();
        }

        if (Input.GetKeyDown(teclaDebugReiniciar))
        {
            Reiniciar();
        }
    }

    void TerminarCalibracion()
    {
        calibrando = false;
        editor.EstablecerEdicion(false);

        if (experienciaIniciada)
        {
            VolcarEstado();
        }
        else
        {
            EstadoInicial();
        }

        if (servidor != null && !servidor.enabled)
        {
            servidor.enabled = true;
        }

        Debug.Log("Mapping: calibracion terminada, proyeccion activa.");
    }

    void EntrarCalibracion()
    {
        calibrando = true;
        editor.EstablecerEdicion(true);

        foreach (string nombre in editor.ObtenerNombresSuperficies())
        {
            editor.CambiarContenido(nombre, null);
        }

        Debug.Log("Mapping: modo calibracion.");
    }

    public void Proyectar(int fase, int indice)
    {
        List<ContenidoSuperficie> lista = fase == 1 ? fase1 : fase2;

        if (indice < 0 || indice >= lista.Count)
        {
            Debug.LogWarning("Mapping: no hay contenido para fase " + fase + ", indice " + indice + ".");
            return;
        }

        experienciaIniciada = true;
        Aplicar(lista[indice]);
        Debug.Log("Mapping: fase " + fase + ", objeto " + indice + " en " + lista[indice].nombreSuperficie + ".");
    }

    public void IniciarFinal()
    {
        experienciaIniciada = true;

        foreach (ContenidoSuperficie contenido in contenidoFinal)
        {
            Aplicar(contenido);
        }

        Debug.Log("Mapping: contenido final.");
    }

    public void Reiniciar()
    {
        if (!listo)
        {
            return;
        }

        experienciaIniciada = false;
        pasoDebug = 0;
        EstadoInicial();
        Debug.Log("Mapping: reiniciado al estado inicial.");
    }

    void DebugSiguiente()
    {
        int totalFases = fase1.Count + fase2.Count;

        if (pasoDebug < fase1.Count)
        {
            Proyectar(1, pasoDebug);
        }
        else if (pasoDebug < totalFases)
        {
            Proyectar(2, pasoDebug - fase1.Count);
        }
        else if (pasoDebug == totalFases)
        {
            IniciarFinal();
        }
        else
        {
            Debug.Log("Mapping debug: no quedan pasos. " + teclaDebugReiniciar + " para reiniciar.");
            return;
        }

        pasoDebug++;
    }

    void EstadoInicial()
    {
        foreach (KeyValuePair<string, VideoPlayer> par in reproductores)
        {
            par.Value.Stop();
        }

        estado.Clear();

        foreach (string nombre in editor.ObtenerNombresSuperficies())
        {
            Asignar(nombre, vacio);
        }

        foreach (ContenidoSuperficie contenido in fijas)
        {
            Aplicar(contenido);
        }
    }

    void VolcarEstado()
    {
        foreach (string nombre in editor.ObtenerNombresSuperficies())
        {
            if (!estado.ContainsKey(nombre))
            {
                estado[nombre] = vacio;
            }
        }

        foreach (KeyValuePair<string, Texture> par in estado)
        {
            if (!editor.CambiarContenido(par.Key, par.Value))
            {
                Debug.LogWarning("Mapping: superficie no encontrada: " + par.Key);
            }
        }
    }

    void Aplicar(ContenidoSuperficie contenido)
    {
        if (contenido == null || string.IsNullOrWhiteSpace(contenido.nombreSuperficie))
        {
            return;
        }

        string nombre = contenido.nombreSuperficie;

        if (contenido.video != null)
        {
            Asignar(nombre, PrepararVideo(nombre, contenido.video));
            return;
        }

        DetenerVideo(nombre);
        Asignar(nombre, contenido.imagen != null ? contenido.imagen : vacio);
    }

    void Asignar(string nombre, Texture textura)
    {
        estado[nombre] = textura;

        if (calibrando)
        {
            return;
        }

        if (!editor.CambiarContenido(nombre, textura))
        {
            Debug.LogWarning("Mapping: superficie no encontrada: " + nombre);
        }
    }

    Texture PrepararVideo(string nombre, VideoClip clip)
    {
        VideoPlayer reproductor;

        if (!reproductores.TryGetValue(nombre, out reproductor))
        {
            GameObject objeto = new GameObject("Video_" + nombre);
            objeto.transform.SetParent(transform, false);
            reproductor = objeto.AddComponent<VideoPlayer>();
            reproductor.playOnAwake = false;
            reproductor.isLooping = true;
            reproductor.renderMode = VideoRenderMode.RenderTexture;
            reproductor.audioOutputMode = VideoAudioOutputMode.None;
            reproductores[nombre] = reproductor;
        }

        int ancho = Mathf.Max(16, (int)clip.width);
        int alto = Mathf.Max(16, (int)clip.height);
        RenderTexture textura;

        if (!texturasVideo.TryGetValue(nombre, out textura) || textura.width != ancho || textura.height != alto)
        {
            reproductor.Stop();
            reproductor.targetTexture = null;

            if (textura != null)
            {
                textura.Release();
                Destroy(textura);
            }

            textura = new RenderTexture(ancho, alto, 0);
            textura.name = "Video_" + nombre;
            texturasVideo[nombre] = textura;
        }

        RenderTexture anterior = RenderTexture.active;
        RenderTexture.active = textura;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = anterior;

        reproductor.Stop();
        reproductor.clip = clip;
        reproductor.targetTexture = textura;
        reproductor.Play();

        return textura;
    }

    void DetenerVideo(string nombre)
    {
        VideoPlayer reproductor;

        if (reproductores.TryGetValue(nombre, out reproductor))
        {
            reproductor.Stop();
        }
    }

    void OnGUI()
    {
        if (!listo || (!calibrando && !modoDebug))
        {
            return;
        }

        if (estiloDebug == null)
        {
            estiloDebug = new GUIStyle(GUI.skin.label);
            estiloDebug.fontSize = 16;
            estiloDebug.normal.textColor = Color.yellow;
        }

        string texto;

        if (calibrando)
        {
            texto = "CALIBRACION | ENTER: guardar | " + teclaCalibracion + ": empezar la experiencia";
        }
        else
        {
            int total = fase1.Count + fase2.Count + 1;
            texto = "MAPPING DEBUG | paso " + Mathf.Min(pasoDebug, total) + " de " + total + " | " + teclaDebugSiguiente + ": siguiente | " + teclaDebugReiniciar + ": reiniciar | " + teclaCalibracion + ": calibrar";
        }

        GUI.Label(new Rect(12, Screen.height - 34, Screen.width - 24, 28), texto, estiloDebug);
    }

    void OnDestroy()
    {
        foreach (KeyValuePair<string, RenderTexture> par in texturasVideo)
        {
            if (par.Value != null)
            {
                par.Value.Release();
                Destroy(par.Value);
            }
        }

        if (vacio != null)
        {
            Destroy(vacio);
        }
    }
}
