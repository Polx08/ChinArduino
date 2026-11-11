
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MappingTexturasPrueba : MonoBehaviour
{
    [Serializable]
    public class AsignacionTextura
    {
        public string nombreSuperficie = "Superficie_01";
        public Texture textura;
    }

    [Header("Editor de Mapping")]
    [SerializeField]
    private MultiSurfaceMappingController editor;

    [Header("Texturas por superficie")]
    [SerializeField]
    private List<AsignacionTextura> asignaciones =
        new List<AsignacionTextura>();

    [Header("Controles")]
    [SerializeField]
    private KeyCode teclaReaplicar = KeyCode.P;

    private IEnumerator Start()
    {
        if (editor == null)
            editor = GetComponent<MultiSurfaceMappingController>();

        if (editor == null)
            editor = FindFirstObjectByType<MultiSurfaceMappingController>();

        // Esperar a que el editor cargue sus superficies.
        yield return null;

        AplicarTexturas();
    }

    private void Update()
    {
        if (Input.GetKeyDown(teclaReaplicar))
            AplicarTexturas();
    }

    [ContextMenu("Aplicar texturas")]
    public void AplicarTexturas()
    {
        if (editor == null)
        {
            Debug.LogError(
                "[CamPlus Mapping] No se encontró el editor."
            );
            return;
        }

        foreach (AsignacionTextura asignacion in asignaciones)
        {
            if (asignacion == null ||
                string.IsNullOrWhiteSpace(asignacion.nombreSuperficie))
                continue;

            bool resultado = editor.CambiarContenido(
                asignacion.nombreSuperficie,
                asignacion.textura
            );

            if (resultado)
            {
                Debug.Log(
                    "[CamPlus Mapping] Textura aplicada a: " +
                    asignacion.nombreSuperficie
                );
            }
            else
            {
                Debug.LogWarning(
                    "[CamPlus Mapping] Superficie no encontrada: " +
                    asignacion.nombreSuperficie
                );
            }
        }
    }
}
