# ChanceCast

Aplicación de escritorio para Windows que cuenta intentos por sesión
(sirve para cofres, pulls de gacha, shinies o cualquier evento repetible),
mantiene un historial local y estima probabilidades a partir de una tasa configurable.

La probabilidad predeterminada es de **1 entre 700 000** por intento. Es un dato
proporcionado por el usuario, **no** una probabilidad verificada por la aplicación.

## Uso (usuario final)

1. Copiá `ChanceCast.exe` (carpeta `artifacts/publish-single`) a donde quieras.
2. Abrilo. No necesita instalar .NET, Visual Studio ni Python.
3. **Contador**: `Iniciar sesión` → `+1` por intento (o tu atajo) → `Terminar sesión`.
4. **Historial**: editar o eliminar registros, exportar CSV.
5. **Estadísticas**: actividad, probabilidad y proyecciones.
6. **Configuración**: nombre del evento, denominador (con su equivalente en %),
   tema claro/oscuro, siempre visible, confirmación al terminar y atajos de
   teclado (locales siempre; `+1`/`−1` opcionales como globales aunque otra
   app esté enfocada).

Los datos viven en `%LOCALAPPDATA%\ChanceCast\` (`data.json`,
`data.backup.json`, `logs\`). Podés mover o reemplazar el `.exe` sin perder datos.

> Estas estimaciones suponen intentos independientes y una probabilidad constante.
> El historial no aumenta la probabilidad del siguiente intento.

## Notas y límites conocidos

- Sin firma de código todavía: Windows SmartScreen / Smart App Control pueden
  mostrar una advertencia la primera vez (el ejecutable no tiene reputación).
  Verificá que el archivo venga de tu propia publicación.
- Los atajos globales usan `RegisterHotKey` de Windows: solo reciben el aviso
  del sistema operativo, no leen ni modifican el juego.
- No hay detección automática de intentos, overlay sobre el juego, cuentas ni nube.

## Desarrollo

- SDK de .NET 10 (`10.0.4xx`, ver `Dockerfile.test`), Windows para la app WPF.
- Solución: `ChanceCast.slnx` con `src/ChanceCast.Core` (lógica, sin WPF),
  `src/ChanceCast.App` (ViewModels testeables), `src/ChanceCast.Desktop`
  (WPF + Win32) y dos proyectos de xUnit.
- Tests de `Core` y `App`: corren en **WSL/Linux** (el host con Smart App
  Control activado bloquea aleatoriamente los DLL recién compilados):
  `~/.dotnet/dotnet test tests/ChanceCast.Core.Tests/...` y el de Desktop.Tests.
- Imagen de tests: `docker build -f Dockerfile.test -t chancecast-test . && docker run --rm chancecast-test`
  (requiere Docker Desktop con daemon corriendo).
- Publicar: `dotnet publish src/ChanceCast.Desktop -c Release -r win-x64
  --self-contained true -p:PublishSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
  -o artifacts/publish-single`
