# Sistema de Unificación de Reportes de Asistencia para la Procuraduría General del Estado, Ecuador.

## Introducción

Este proyecto es una solución de software integral desarrollada para la **Procuraduría General del Estado de Ecuador (PGE)**. El sistema automatiza el proceso de consolidación de reportes de asistencia de los empleados, solucionando un problema crítico de incompatibilidad entre dos sistemas legados diferentes y eliminando la necesidad de un procesamiento manual propenso a errores.

## El Problema

El departamento de Recursos Humanos de la Procuraduría General del Estado enfrentaba un desafío operativo significativo:
* **Sin Acceso a la Base de Datos:** Para desarrollar este sistema no pudimos acceder directamente a las base de datos de ambos sistemas por lo que optamos por leer los reportes que emitian para extraer la data y unificarla.
* **Formato de reportes:** Las marcaciones de asistencia se generaban en dos sistemas separados, el biometrico exportaba los reportes en excel, mientras que el sistema interno Siath lo hacia en formato pdf el cual era complicado analizarlo.
* **Proceso Manual y Lento:** La unificación de estos reportes se realizaba manualmente, un proceso que consumía mucho tiempo y era altamente susceptible a errores humanos.
* **Inconsistencias en los Datos:** Los formatos de nombres y la estructura de los datos eran diferentes entre los dos reportes, complicando aún más la consolidación.

---

## La Solución

Se desarrolló una solución completa basada en .NET que automatiza todo el flujo de trabajo, desde la lectura de los archivos hasta la generación de un reporte final unificado y listo para su uso.

### Características Principales

* **Extracción Automatizada de Datos:**
    * Dos **Workers de consola** independientes monitorean carpetas locales.
    * Defido a la dificultad de leer la informacion no estructurada del pdf que exportaba el sistema Siath, se solicitó al equipo de desarrollo de la Procuraduría General del Estado añadir una opción para que el Siah pueda exportar también en excel, con una estructura de columnas adecuada para el análisis requerido.
    * Ambos workers procesan los **reportes en Excel**, manejando los formatos de fecha y datos específicos del archivo.
* **API Centralizada:**
    * Una **API RESTful en ASP.NET Core** actúa como el núcleo del sistema, recibiendo y almacenando los datos de ambos workers.
* **Lógica de Unificación Inteligente:**
    * Un **servicio de unificación** avanzado que contiene la lógica de negocio para consolidar los registros.
    * **Match de Nombres Preciso:** Implementa un algoritmo para identificar y asociar correctamente a los empleados.
    * **Reglas de Negocio Aplicadas:** Prioriza los datos del sistema más fiable (SEAT), rellena los huecos con la información del biométrico y aplica reglas de negocio específicas (márgenes de atraso, horarios de almuerzo).
* **Visualización y Exportación:**
    * Una **interfaz web simple (HTML, JS, Bootstrap)** permite al personal de RRHH buscar registros por rango de fechas y visualizarlos en pantalla.
    * La funcionalidad de **exportar a Excel** genera un reporte `.xlsx` profesional y formateado con los datos ya consolidados, listo para ser utilizado.

---

## Arquitectura y Stack Tecnológico

| Componente      | Tecnologías Utilizadas                                                                          |
| --------------- | ----------------------------------------------------------------------------------------------- |
| **Backend** | C#, .NET, ASP.NET Core Web API, Entity Framework Core                                           |
| **Base de Datos** | SQLite (ligera y perfecta para despliegue en una sola máquina)                                  |
| **Workers** | Aplicaciones de Consola .NET                                                                    |
| **Lectura de Archivos** | `UglyToad.PdfPig` (para PDF), `ExcelDataReader` (para Excel)                                    |
| **Frontend** | HTML5, CSS, Bootstrap 5, JavaScript (Vanilla JS con Fetch API)                                 |

---

## Estructura del Proyecto

La solución está organizada en varios proyectos para una clara separación de responsabilidades:

* `apiCrud/`: El proyecto principal de la API que contiene los Controladores, Servicios (Unificación, Exportación a Excel), DTOs y la configuración de la base de datos.
* `WorkerSeat/`: El worker de consola encargado de procesar los reportes en formato Excel del sistema SEAT.
* `WorkerBiometrico/`: El worker de consola encargado de procesar los reportes en formato Excel del sistema Biométrico.

---

## Cómo Ejecutar el Proyecto

1.  **Clonar el Repositorio:**
    ```bash
    git clone https://github.com/RonnieCordova/git-pge-proyect.git
    ```
2.  **Configurar la Base de Datos:**
    * Navega a la carpeta del proyecto `apiCrud`.
    * Ejecuta las migraciones de Entity Framework para crear la base de datos SQLite:
    ```bash
    dotnet ef database update
    ```
3.  **Ejecutar la API:**
    * En la misma terminal, inicia la API:
    ```bash
    dotnet run
    ```
4.  **Ejecutar los Workers:**
    * Abre **nuevas terminales** para cada proyecto de worker (`WorkerSiath` y `WorkerBiometrico`).
    * Asegúrate de que los archivos de reporte (Excel) se encuentren en las carpetas especificadas en el código.
    * Ejecuta cada worker:
    ```bash
    dotnet run
    ```
5.  **Abrir el Frontend:**
    * Busca el archivo `index.html` en tu explorador de archivos y ábrelo con cualquier navegador web.