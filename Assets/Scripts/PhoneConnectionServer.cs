using UnityEngine;
using TMPro;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class PhoneConnectionServer : MonoBehaviour
{
    [Header("Serial compartido")]
    public ArduinoSerialManager serial;

    [Header("Red")]
    public int puerto = 7777;

    [Header("Animacion de conectado")]
    public Color colorConectado = new Color(0f, 1f, 0.6f);

    [Header("UI (opcional)")]
    public TMP_Text textoEstado;
    public TMP_Text textoIp;

    [Header("Debug sin telefono")]
    public KeyCode teclaDebugConectar = KeyCode.C;

    public event Action OnTelefonoConectado;
    public event Action OnTelefonoDesconectado;
    public event Action<string> OnMensajeRecibido;

    public bool TelefonoConectado { get; private set; }

    readonly ConcurrentQueue<Action> accionesPrincipales = new ConcurrentQueue<Action>();
    readonly object lockCliente = new object();

    TcpListener listener;
    Thread hiloAceptar;
    TcpClient cliente;
    StreamWriter escritor;
    volatile bool corriendo;

    void OnEnable()
    {
        IniciarServidor();
    }

    void OnDisable()
    {
        DetenerServidor();
    }

    void Update()
    {
        while (accionesPrincipales.TryDequeue(out Action accion))
        {
            accion();
        }

        if (Input.GetKeyDown(teclaDebugConectar))
        {
            MarcarConectado();
        }
    }

    void IniciarServidor()
    {
        TelefonoConectado = false;
        ActualizarEstado("Esperando telefono...");

        string ip = ObtenerIpLocal();
        string info = "IP de la PC: " + ip + "  |  Puerto: " + puerto;
        Debug.Log(info);

        if (textoIp != null)
        {
            textoIp.text = info;
        }

        try
        {
            listener = new TcpListener(IPAddress.Any, puerto);
            listener.Start();
            corriendo = true;
            hiloAceptar = new Thread(BucleAceptar) { IsBackground = true };
            hiloAceptar.Start();
        }
        catch (Exception e)
        {
            Debug.LogWarning("No se pudo iniciar el servidor (puerto " + puerto + " en uso?): " + e.Message);
            ActualizarEstado("Error al iniciar el servidor");
        }
    }

    void DetenerServidor()
    {
        corriendo = false;

        try
        {
            if (listener != null)
            {
                listener.Stop();
            }
        }
        catch (Exception) { }

        listener = null;

        lock (lockCliente)
        {
            try
            {
                if (cliente != null)
                {
                    cliente.Close();
                }
            }
            catch (Exception) { }

            cliente = null;
            escritor = null;
        }

        TelefonoConectado = false;
    }

    void BucleAceptar()
    {
        while (corriendo)
        {
            try
            {
                TcpClient nuevo = listener.AcceptTcpClient();
                Thread hilo = new Thread(() => AtenderCliente(nuevo)) { IsBackground = true };
                hilo.Start();
            }
            catch (Exception)
            {
                if (!corriendo)
                {
                    break;
                }

                Thread.Sleep(100);
            }
        }
    }

    void AtenderCliente(TcpClient nuevo)
    {
        try
        {
            NetworkStream stream = nuevo.GetStream();
            StreamReader lector = new StreamReader(stream);
            StreamWriter nuevoEscritor = new StreamWriter(stream) { AutoFlush = true };

            lock (lockCliente)
            {
                if (cliente != null)
                {
                    try { cliente.Close(); } catch (Exception) { }
                }

                cliente = nuevo;
                escritor = nuevoEscritor;
            }

            accionesPrincipales.Enqueue(MarcarConectado);

            string linea;
            while (corriendo && (linea = lector.ReadLine()) != null)
            {
                string mensaje = linea.Trim();
                accionesPrincipales.Enqueue(() => OnMensajeRecibido?.Invoke(mensaje));
            }
        }
        catch (Exception) { }
        finally
        {
            bool eraElActual;

            lock (lockCliente)
            {
                eraElActual = ReferenceEquals(cliente, nuevo);

                if (eraElActual)
                {
                    cliente = null;
                    escritor = null;
                }
            }

            try { nuevo.Close(); } catch (Exception) { }

            if (eraElActual)
            {
                accionesPrincipales.Enqueue(MarcarDesconectado);
            }
        }
    }

    void MarcarConectado()
    {
        TelefonoConectado = true;
        EnviarAlTelefono("CONECTADO");

        if (serial != null)
        {
            serial.EnviarComando("CONECTADO:" + ColorUtility.ToHtmlStringRGB(colorConectado));
        }

        ActualizarEstado("Telefono conectado");
        OnTelefonoConectado?.Invoke();
    }

    void MarcarDesconectado()
    {
        TelefonoConectado = false;
        ActualizarEstado("Telefono desconectado, esperando...");
        OnTelefonoDesconectado?.Invoke();
    }

    public void EnviarAlTelefono(string mensaje)
    {
        lock (lockCliente)
        {
            if (escritor == null)
            {
                return;
            }

            try
            {
                escritor.WriteLine(mensaje);
            }
            catch (Exception) { }
        }
    }

    void ActualizarEstado(string texto)
    {
        Debug.Log(texto);

        if (textoEstado != null)
        {
            textoEstado.text = texto;
        }
    }

    string ObtenerIpLocal()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint punto = socket.LocalEndPoint as IPEndPoint;

                if (punto != null)
                {
                    return punto.Address.ToString();
                }
            }
        }
        catch (Exception) { }

        try
        {
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception) { }

        return "desconocida";
    }
}
