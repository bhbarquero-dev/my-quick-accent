# my-quick-accent

El menú de acentos de Mac, en Windows: mantenga presionada una letra y aparece una burbuja junto al
texto con las variantes acentuadas; elija con el número.

## Uso

- **Mantener presionada** `a e i o u n c s y l z` → aparece la burbuja.
- **1–9** elige la opción. También `←` `→` y `Enter`, o clic.
- **Esc**, o seguir escribiendo, la cierra.
- Con Shift o Bloq Mayús salen las mayúsculas.

Ícono `á` en la bandeja del sistema (clic derecho): **Pausar**, **Iniciar con Windows**, **Salir**.

## Requisitos

- Windows 10 u 11.
- [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0). Verifique con `dotnet --version`.

## Probar desde el repositorio

Sirve para probar cambios sin instalar nada.

1. Si ya hay una copia corriendo, ciérrela (ícono `á` de la bandeja > **Salir**). Solo puede haber una
   instancia a la vez; una segunda se cierra sola sin avisar.
2. Desde la carpeta del repositorio, compile y ejecute:

   ```powershell
   dotnet run -c Release
   ```

3. Confirme que aparece el ícono `á` en la bandeja del sistema (puede estar bajo la flecha `^`).
4. Abra un editor de texto, deje presionada la `a` y elija una opción con un número.
5. Para detenerla: `Ctrl+C` en la terminal, o **Salir** en el ícono de la bandeja.

El ejecutable queda en `bin\Release\net10.0-windows\MyQuickAccent.exe`, por si prefiere abrirlo directo.

## Instalar como app

1. Desde la carpeta del repositorio, ejecute el instalador:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\install.ps1
   ```

   El script cierra la copia que esté corriendo, compila, copia la app a
   `%LOCALAPPDATA%\Programs\MyQuickAccent` y la arranca. No pide permisos de administrador.
2. Clic derecho en el ícono `á` de la bandeja > **Iniciar con Windows**, para que arranque sola en cada
   inicio de sesión.

**Actualizar:** traiga los cambios (`git pull`) y vuelva a ejecutar `install.ps1`.

**Desinstalar:**

1. Clic derecho en el ícono > desmarque **Iniciar con Windows**.
2. Clic derecho en el ícono > **Salir**.
3. Borre la carpeta `%LOCALAPPDATA%\Programs\MyQuickAccent`.

## Personalizar

Las letras y el orden de las opciones están en la tabla `Accents` de [Program.cs](Program.cs).

## Limitaciones

- En las letras de la tabla se pierde la repetición al dejar la tecla presionada (igual que en Mac).
  Para juegos, use **Pausar**.
- No funciona dentro de ventanas que corren como administrador.
- Si una app no informa dónde está el cursor de texto, la burbuja sale centrada en la ventana.
