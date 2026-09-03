using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class RFIDManager : MonoBehaviour
{
    [Header("Serial compartido")]
    public ArduinoSerialManager serial;

    [Header("Base de datos")]
    public RFIDDatabase database;

    [Header("Referencias UI")]
    public Image imagenUI;
    public AudioSource audioSource;

    [Header("Bloqueo inicial (opcional)")]
    [Tooltip("Si se asigna, el RFID no responde hasta que este sensor se active (ej: abrir el cofre)")]
    public Proximidad sensorInicio;

    [Header("Cola de reproduccion")]
    [Tooltip("Tiempo minimo entre lecturas de la misma id, para evitar que una sola pasada se registre varias veces")]
    public float cooldownRelectura = 1.5f;

    int seccionActual = 1;
    bool reproduciendo = false;
    bool ledsApagados = false;
    bool cofreAbierto = false;
    HashSet<string> idsCompletados = new HashSet<string>();
    Queue<IEnumerator> cola = new Queue<IEnumerator>();
    Dictionary<string, float> ultimaLectura = new Dictionary<string, float>();

    void Start()
    {
        if (imagenUI != null)
        {
            imagenUI.enabled = false;
        }

        if (sensorInicio != null)
        {
            sensorInicio.OnActivado += () => cofreAbierto = true;
        }
        else
        {
            cofreAbierto = true;
        }

        if (serial != null)
        {
            serial.OnLineaRecibida += ProcesarId;
            serial.OnConectado += ApagarLedsAlConectar;
        }
    }

    void ApagarLedsAlConectar()
    {
        if (!ledsApagados)
        {
            ledsApagados = true;
            serial.EnviarComando("LED:OFF");
        }
    }

    void Update()
    {
        if (database == null || serial == null || !serial.ModoTeclado || !cofreAbierto)
        {
            return;
        }

        foreach (var entrada in ListaActiva())
        {
            if (entrada.teclaDebug != KeyCode.None && Input.GetKeyDown(entrada.teclaDebug))
            {
                ProcesarId(entrada.id);
            }
        }
    }

    List<RFIDEntry> ListaActiva()
    {
        return seccionActual == 1 ? database.seccion1 : database.seccion2;
    }

    void ProcesarId(string id)
    {
        if (database == null || string.IsNullOrEmpty(id))
        {
            return;
        }

        if (!Regex.IsMatch(id, @"^[0-9A-Fa-f]+$"))
        {
            return;
        }

        Debug.Log("RFID leido: " + id);

        if (!cofreAbierto)
        {
            return;
        }

        if (ultimaLectura.TryGetValue(id, out float tiempoAnterior) && Time.time - tiempoAnterior < cooldownRelectura)
        {
            return;
        }

        List<RFIDEntry> listaActiva = ListaActiva();
        RFIDEntry entrada = RFIDDatabase.BuscarPorId(listaActiva, id);

        if (entrada == null)
        {
            return;
        }

        ultimaLectura[id] = Time.time;
        idsCompletados.Add(id);
        cola.Enqueue(ReproducirEntrada(entrada));

        if (SeccionCompleta(listaActiva))
        {
            cola.Enqueue(CompletarSeccion());
        }

        if (!reproduciendo)
        {
            StartCoroutine(ProcesarCola());
        }
    }

    bool SeccionCompleta(List<RFIDEntry> lista)
    {
        foreach (var entrada in lista)
        {
            if (!idsCompletados.Contains(entrada.id))
            {
                return false;
            }
        }
        return lista.Count > 0;
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

    IEnumerator ReproducirEntrada(RFIDEntry entrada)
    {
        if (imagenUI != null && entrada.imagen != null)
        {
            imagenUI.sprite = entrada.imagen;
            imagenUI.enabled = true;
        }

        if (entrada.ledsIndices.Count > 0 && serial != null)
        {
            string indicesStr = string.Join(",", entrada.ledsIndices);
            string colorHex = ColorUtility.ToHtmlStringRGB(entrada.colorLeds);
            serial.EnviarComando("LED:" + indicesStr + ":" + colorHex);
        }

        float duracion = entrada.duracionImagen > 0f ? entrada.duracionImagen : 0f;

        if (audioSource != null && entrada.audio != null)
        {
            audioSource.clip = entrada.audio;
            audioSource.Play();

            if (entrada.duracionImagen <= 0f)
            {
                duracion = entrada.audio.length;
            }
        }

        yield return new WaitForSeconds(duracion);

        if (imagenUI != null)
        {
            imagenUI.enabled = false;
        }
    }

    IEnumerator CompletarSeccion()
    {
        if (serial != null)
        {
            serial.EnviarComando("LED:OFF");
        }

        AudioClip audioClip = seccionActual == 1 ? database.audioFinalSeccion1 : database.audioFinal;

        if (seccionActual == 1)
        {
            seccionActual = 2;
            idsCompletados.Clear();
        }

        if (audioSource != null && audioClip != null)
        {
            audioSource.clip = audioClip;
            audioSource.Play();
            yield return new WaitForSeconds(audioClip.length);
        }
    }

    void OnDisable()
    {
        if (serial != null)
        {
            serial.OnLineaRecibida -= ProcesarId;
            serial.OnConectado -= ApagarLedsAlConectar;
        }
    }
}