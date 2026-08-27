using UnityEngine;
using System.IO.Ports;

public class InpurManager : MonoBehaviour
{
    SerialPort stream = new SerialPort("COM3", 9600);

    [Header("Configuracion Visual")]
    public SpriteRenderer spriteAEditar;
    public Color colorNormal = Color.white;
    public Color colorPresionado = Color.green;

    [Header("Debug sin Arduino")]
    public bool forzarModoTeclado = false;
    public KeyCode teclaDebug = KeyCode.Space;

    bool modoTeclado = false;

    void Start()
    {
        if (stream != null && stream.IsOpen)
        {
            stream.Close();
        }

        if (forzarModoTeclado)
        {
            modoTeclado = true;
            Debug.Log("Modo teclado forzado, no se intenta abrir el puerto serial.");
        }
        else
        {
            try
            {
                stream.Open();
                stream.ReadTimeout = 25;
                Debug.Log("Puerto abierto exitosamente.");
            }
            catch (System.Exception e)
            {
                modoTeclado = true;
                Debug.LogWarning("No se pudo abrir el puerto, usando modo teclado: " + e.Message);
            }
        }

        if (spriteAEditar == null)
        {
            spriteAEditar = GetComponent<SpriteRenderer>();
        }
    }

    void Update()
    {
        if (modoTeclado)
        {
            spriteAEditar.color = Input.GetKey(teclaDebug) ? colorPresionado : colorNormal;
            return;
        }

        if (stream.IsOpen)
        {
            try
            {
                string dato = stream.ReadLine();

                if (dato.Contains("1"))
                {
                    spriteAEditar.color = colorPresionado;
                }
                else if (dato.Contains("0"))
                {
                    spriteAEditar.color = colorNormal;
                }
            }
            catch (System.Exception) { }
        }
    }

    void OnDisable()
    {
        if (stream != null && stream.IsOpen)
        {
            stream.Close();
            Debug.Log("Puerto serial cerrado correctamente.");
        }
    }
}