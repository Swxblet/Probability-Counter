# Contador de Cofres

Aplicación de escritorio para Windows que cuenta cofres abiertos por partida,
mantiene un historial local y estima probabilidades de obtener un ítem a partir
de una tasa configurable.

La probabilidad predeterminada es de **1 entre 700 000** por intento. Es un dato
proporcionado por el usuario, **no** una probabilidad de Fortnite verificada por
la aplicación.

## Uso (usuario final)

1. Copiá `ContadorCofres.exe` (carpeta `artifacts/publish-single`) a donde quieras.
2. Abrilo. No necesita instalar .NET, Visual Studio ni Python.
3. **Contador**: `Iniciar partida` → `+1` por cofre (o tu atajo) → `Terminar partida`.
4. **Historial**: editar o eliminar registros, exportar CSV.
5. **Estadísticas**: actividad, probabilidad y proyecciones.
6. **Configuración**: nombre del ítem, denominador, siempre visible, confirmación
   al terminar y atajos de teclado (locales siempre; `+1`/`−1` opcionales como
   globales aunque el juego esté enfocado).

Los datos viven en `%LOCALAPPDATA%\ContadorCofres\` (`data.json`,
`data.backup.json`, `logs\`). Podés mover o reemplazar el `.exe` sin perder datos.

> Estas estimaciones suponen intentos independientes y una probabilidad constante.
> El historial no aumenta la probabilidad del siguiente cofre.

## Notas y límites conocidos

- Sin firma de código todavía: Windows SmartScreen / Smart App Control pueden
  mostrar una advertencia la primera vez (el ejecutable no tiene reputación).
  Verificá que el archivo venga de tu propia publicación.
- Los atajos globales usan `RegisterHotKey` de Windows: solo reciben el aviso
  del sistema operativo, no leen ni modifican el juego.
- No hay detección automática de cofres, overlay sobre el juego, cuentas ni nube.

## Desarrollo

- SDK de .NET 10 (`10.0.4xx`, ver `Dockerfile.test`), Windows para la app WPF.
- Solución: `ContadorCofres.slnx` con `src/ContadorCofres.Core` (lógica, sin WPF),
  `src/ContadorCofres.App` (ViewModels testeables), `src/ContadorCofres.Desktop`
  (WPF + Win32) y dos proyectos de xUnit.
- Tests de `Core` y `App`: corren en **WSL/Linux** (el host con Smart App
  Control activado bloquea aleatoriamente los DLL recién compilados):
  `~/.dotnet/dotnet test tests/ContadorCofres.Core.Tests/...` y el de Desktop.Tests.
- Imagen de tests: `docker build -f Dockerfile.test -t cofres-test . && docker run --rm cofres-test`
  (requiere Docker Desktop con daemon corriendo).
- Publicar: `dotnet publish src/ContadorCofres.Desktop -c Release -r win-x64
  --self-contained true -p:PublishSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
  -o artifacts/publish-single`
