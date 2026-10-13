using System.Runtime.InteropServices;
using Tmds.DBus.Protocol;

namespace Tela.Platform;

/// <summary>
/// Não deixa o ecrã apagar (nem o PC adormecer) enquanto há vídeo a tocar. No Windows, pela
/// SetThreadExecutionState; no Linux, pelo portal do ambiente de trabalho (GNOME, KDE…) e, se
/// não houver portal, pelo serviço org.freedesktop.ScreenSaver.
/// </summary>
public sealed class ScreenSaver : IDisposable
{
    private bool _wanted;
    private bool _applied;
    private readonly SemaphoreSlim _linuxGate = new(1, 1);
    private Connection? _bus;
    private string? _portalRequest;
    private uint? _cookie;

    /// <summary>Chamar da thread da interface (no Windows o estado é da thread que o pede).</summary>
    public void Set(bool inhibit)
    {
        _wanted = inhibit;
        if (OperatingSystem.IsWindows())
        {
            if (_applied == inhibit)
                return;
            _applied = inhibit;
            SetThreadExecutionState(inhibit ? EsContinuous | EsSystemRequired | EsDisplayRequired : EsContinuous);
        }
        else if (OperatingSystem.IsLinux())
            _ = SyncLinuxAsync();
    }

    private async Task SyncLinuxAsync()
    {
        await _linuxGate.WaitAsync();
        try
        {
            var wanted = _wanted;
            if (wanted == _applied)
                return;
            if (wanted)
                await InhibitAsync();
            else
                await ReleaseAsync();
            _applied = wanted;
        }
        catch (Exception ex)
        {
            Log.Write("Ecrã ligado (D-Bus): " + ex.Message);
            _applied = _wanted;
        }
        finally
        {
            _linuxGate.Release();
        }
    }

    private async Task<Connection?> BusAsync()
    {
        if (_bus is not null)
            return _bus;
        if (string.IsNullOrEmpty(Address.Session))
            return null;
        var bus = new Connection(Address.Session!);
        await bus.ConnectAsync();
        return _bus = bus;
    }

    private async Task InhibitAsync()
    {
        var bus = await BusAsync();
        if (bus is null)
            return;
        try
        {
            _portalRequest = await bus.CallMethodAsync(PortalInhibit(bus),
                (message, _) => message.GetBodyReader().ReadObjectPath().ToString());
            return;
        }
        catch (DBusException)
        {
            // Sem portal: tenta-se o serviço clássico.
        }
        _cookie = await bus.CallMethodAsync(ClassicInhibit(bus), (message, _) => message.GetBodyReader().ReadUInt32());
    }

    // O MessageWriter não atravessa um await: as mensagens fazem-se em funções à parte.
    private static MessageBuffer PortalInhibit(Connection bus)
    {
        // Inhibit(janela, flags, opções); 8 = não marcar a sessão como inativa.
        using var writer = bus.GetMessageWriter();
        writer.WriteMethodCallHeader("org.freedesktop.portal.Desktop", "/org/freedesktop/portal/desktop",
            "org.freedesktop.portal.Inhibit", "Inhibit", "sua{sv}");
        writer.WriteString("");
        writer.WriteUInt32(8);
        var dict = writer.WriteDictionaryStart();
        writer.WriteDictionaryEntryStart();
        writer.WriteString("reason");
        writer.WriteVariantString("Playing video");
        writer.WriteDictionaryEnd(dict);
        return writer.CreateMessage();
    }

    private static MessageBuffer ClassicInhibit(Connection bus)
    {
        using var writer = bus.GetMessageWriter();
        writer.WriteMethodCallHeader("org.freedesktop.ScreenSaver", "/org/freedesktop/ScreenSaver",
            "org.freedesktop.ScreenSaver", "Inhibit", "ss");
        writer.WriteString("Tela");
        writer.WriteString("Playing video");
        return writer.CreateMessage();
    }

    private static MessageBuffer PortalClose(Connection bus, string request)
    {
        using var writer = bus.GetMessageWriter();
        writer.WriteMethodCallHeader("org.freedesktop.portal.Desktop", request, "org.freedesktop.portal.Request", "Close");
        return writer.CreateMessage();
    }

    private static MessageBuffer ClassicRelease(Connection bus, uint cookie)
    {
        using var writer = bus.GetMessageWriter();
        writer.WriteMethodCallHeader("org.freedesktop.ScreenSaver", "/org/freedesktop/ScreenSaver",
            "org.freedesktop.ScreenSaver", "UnInhibit", "u");
        writer.WriteUInt32(cookie);
        return writer.CreateMessage();
    }

    private async Task ReleaseAsync()
    {
        var bus = _bus;
        if (bus is null)
            return;
        if (_portalRequest is { } request)
        {
            _portalRequest = null;
            await bus.CallMethodAsync(PortalClose(bus, request));
        }
        if (_cookie is { } cookie)
        {
            _cookie = null;
            await bus.CallMethodAsync(ClassicRelease(bus, cookie));
        }
    }

    public void Dispose()
    {
        Set(false);
        // A ligação ao D-Bus fecha com o processo, e com ela o pedido (se ainda lá estiver).
    }

    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
