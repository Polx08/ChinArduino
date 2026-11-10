using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class ObjetoAR
{
    public string nombre;
    public AudioClip audio;
    public List<int> ledsIndices = new List<int>();
    public Color colorLeds = Color.white;
    public KeyCode teclaDebug = KeyCode.None;
}

public class ExperienciaController : MonoBehaviour
{
    enum Estado { EsperandoCaja, Intro, Fase1, EntreFases, Fase2, Final, Terminado }

    [Header("Referencias")]
    public PhoneConnectionServer servidor;
    public ArduinoSerialManager serial;
    public ProximityAudioTrigger cofre;
    public AudioSource audioSource;
    public AudioSource musicaSource;

    [Header("Fase 1")]
    public List<ObjetoAR> fase1 = new List<ObjetoAR>();
    public AudioClip audioFinalFase1;

    [Header("Fase 2")]
    public List<ObjetoAR> fase2 = new List<ObjetoAR>();

    [Header("Final")]
    public AudioClip audioFinal;
    public AudioClip musicaFinal;
    public Color colorAnimacionFinal = Color.white;

    [Header("Mapping")]
    [Tooltip("Se llama al colocar un objeto con (fase, indice). Conecta aqui tu script de mapping")]
    public UnityEvent<int, int> alProyectar;

    Estado estado = Estado.EsperandoCaja;
    bool reproduciendo = false;
    HashSet<int> colocados = new HashSet<int>();
    Queue<IEnumerator> cola = new Queue<IEnumerator>();

    void OnEnable()
    {
        if (cofre != null)
        {
            cofre.OnActivado += IniciarExperiencia;
        }

        if (servidor != null)
        {
            servidor.OnMensajeRecibido += ProcesarMensaje;
            servidor.OnTelefonoConectado += EnviarEstado;
        }

        if (serial != null)
        {
            serial.OnConectado += ApagarLeds;
        }
    }

    void OnDisable()
    {
        if (cofre != null)
        {
            cofre.OnActivado -= IniciarExperiencia;
        }

        if (servidor != null)
        {
            servidor.OnMensajeRecibido -= ProcesarMensaje;
            servidor.OnTelefonoConectado -= EnviarEstado;
        }

        if (serial != null)
        {
            serial.OnConectado -= ApagarLeds;
        }
    }

    void ApagarLeds()
    {
        serial.EnviarComando("LED:OFF");
    }

    void Update()
    {
        List<ObjetoAR> lista = ListaActiva();

        if (lista == null)
        {
            return;
        }

        int fase = estado == Estado.Fase1 ? 1 : 2;

        for (int i = 0; i < lista.Count; i++)
        {
            if (lista[i].teclaDebug != KeyCode.None && Input.GetKeyDown(lista[i].teclaDebug))
            {
                ProcesarObjeto(fase, i);
            }
        }
    }

    List<ObjetoAR> ListaActiva()
    {
        if (estado == Estado.Fase1)
        {
            return fase1;
        }

        if (estado == Estado.Fase2)
        {
            return fase2;
        }

        return null;
    }

    void IniciarExperiencia()
    {
        if (estado != Estado.EsperandoCaja)
        {
            return;
        }

        estado = Estado.Intro;
        Enviar("CAJA_ABIERTA");
        StartCoroutine(IntroYComenzar());
    }

    IEnumerator IntroYComenzar()
    {
        float duracion = (cofre != null && cofre.clip != null) ? cofre.clip.length : 0f;
        yield return new WaitForSeconds(duracion);
        ComenzarFase(1);
    }

    void ComenzarFase(int fase)
    {
        colocados.Clear();
        estado = fase == 1 ? Estado.Fase1 : Estado.Fase2;

        List<ObjetoAR> lista = fase == 1 ? fase1 : fase2;

        if (lista.Count == 0)
        {
            Debug.LogWarning("La fase " + fase + " no tiene objetos configurados, nunca se va a completar.");
        }

        Enviar("FASE:" + fase + ":" + lista.Count);
    }

    void EnviarEstado()
    {
        switch (estado)
        {
            case Estado.Intro:
                Enviar("CAJA_ABIERTA");
                break;
            case Estado.Fase1:
                Enviar("FASE:1:" + fase1.Count);
                break;
            case Estado.EntreFases:
                Enviar("FASE_FIN:1");
                break;
            case Estado.Fase2:
                Enviar("FASE:2:" + fase2.Count);
                break;
            case Estado.Final:
                Enviar("FASE_FIN:2");
                break;
            case Estado.Terminado:
                Enviar("FIN");
                break;
        }
    }

    void ProcesarMensaje(string mensaje)
    {
        if (mensaje == "DEBUG:CAJA")
        {
            if (cofre != null)
            {
                cofre.ActivarManual();
            }

            return;
        }

        if (!mensaje.StartsWith("OBJETO:"))
        {
            return;
        }

        string[] partes = mensaje.Split(':');

        if (partes.Length == 3 && int.TryParse(partes[1], out int fase) && int.TryParse(partes[2], out int indice))
        {
            ProcesarObjeto(fase, indice);
        }
    }

    void ProcesarObjeto(int fase, int indice)
    {
        int faseEsperada = estado == Estado.Fase1 ? 1 : (estado == Estado.Fase2 ? 2 : 0);

        if (faseEsperada == 0 || fase != faseEsperada)
        {
            Debug.LogWarning("Objeto ignorado (fase " + fase + ", indice " + indice + "): ahora no se esperan objetos de esa fase.");
            return;
        }

        List<ObjetoAR> lista = fase == 1 ? fase1 : fase2;

        if (indice < 0 || indice >= lista.Count)
        {
            Debug.LogWarning("Objeto fuera de rango: fase " + fase + ", indice " + indice + " (la lista tiene " + lista.Count + ").");
            return;
        }

        if (!colocados.Add(indice))
        {
            return;
        }

        LogProgreso(fase, lista);
        cola.Enqueue(ReproducirObjeto(fase, indice, lista[indice]));

        if (colocados.Count >= lista.Count)
        {
            estado = fase == 1 ? Estado.EntreFases : Estado.Final;
            Enviar("FASE_FIN:" + fase);
            cola.Enqueue(fase == 1 ? CerrarFase1() : SecuenciaFinal());
        }

        if (!reproduciendo)
        {
            StartCoroutine(ProcesarCola());
        }
    }

    void LogProgreso(int fase, List<ObjetoAR> lista)
    {
        List<string> pendientes = new List<string>();

        for (int i = 0; i < lista.Count; i++)
        {
            if (!colocados.Contains(i))
            {
                pendientes.Add(string.IsNullOrEmpty(lista[i].nombre) ? "objeto " + i : lista[i].nombre);
            }
        }

        if (pendientes.Count == 0)
        {
            Debug.Log("Fase " + fase + ": todos los objetos colocados.");
        }
        else
        {
            Debug.Log("Fase " + fase + ", faltan (" + pendientes.Count + "): " + string.Join(", ", pendientes));
        }
    }

    IEnumerator ProcesarCola()
    {
        reproduciendo = true;

        while (cola.Count > 0)
        {
            yield return cola.Dequeue();
        }

        reproduciendo = false;
    }

    IEnumerator ReproducirObjeto(int fase, int indice, ObjetoAR objeto)
    {
        alProyectar?.Invoke(fase, indice);

        if (objeto.ledsIndices.Count > 0 && serial != null)
        {
            string indicesStr = string.Join(",", objeto.ledsIndices);
            string colorHex = ColorUtility.ToHtmlStringRGB(objeto.colorLeds);
            serial.EnviarComando("LED:" + indicesStr + ":" + colorHex);
        }

        float duracion = 0f;

        if (audioSource != null && objeto.audio != null)
        {
            audioSource.clip = objeto.audio;
            audioSource.Play();
            duracion = objeto.audio.length;
        }

        yield return new WaitForSeconds(duracion);
    }

    IEnumerator CerrarFase1()
    {
        if (serial != null)
        {
            serial.EnviarComando("LED:OFF");
        }

        if (audioSource != null && audioFinalFase1 != null)
        {
            audioSource.clip = audioFinalFase1;
            audioSource.Play();
            yield return new WaitForSeconds(audioFinalFase1.length);
        }

        ComenzarFase(2);
    }

    IEnumerator SecuenciaFinal()
    {
        float duracionAudio = audioFinal != null ? audioFinal.length : 0f;
        float duracionMusica = musicaFinal != null ? musicaFinal.length : 0f;
        float duracionTotal = Mathf.Max(duracionAudio, duracionMusica);

        if (serial != null)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(colorAnimacionFinal);
            int duracionMs = Mathf.RoundToInt(duracionTotal * 1000f);
            serial.EnviarComando("FINAL:" + colorHex + ":" + duracionMs);
        }

        if (audioSource != null && audioFinal != null)
        {
            audioSource.clip = audioFinal;
            audioSource.Play();
        }

        if (musicaSource != null && musicaFinal != null)
        {
            musicaSource.clip = musicaFinal;
            musicaSource.Play();
        }

        yield return new WaitForSeconds(duracionTotal);

        estado = Estado.Terminado;
        Enviar("FIN");
    }

    void Enviar(string mensaje)
    {
        if (servidor != null)
        {
            servidor.EnviarAlTelefono(mensaje);
        }
    }
}