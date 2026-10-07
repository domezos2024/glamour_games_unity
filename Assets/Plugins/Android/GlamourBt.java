package de.domezosware.glamourgames;

import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothServerSocket;
import android.bluetooth.BluetoothSocket;
import android.util.Log;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Bluetooth-Verbindung (RFCOMM/SPP) fuer Glamour Games: ein Geraet eroeffnet (wartet auf Verbindung), das andere tritt
 * einem gekoppelten Geraet bei. Nachrichten sind UTF-8-Zeilen. Gleiche Dienst-UUID wie die Windows-Version, damit PC
 * und Handy miteinander spielen koennen. Alle blockierenden Aufrufe laufen in eigenen Threads; Unity fragt per poll() ab.
 */
public class GlamourBt {
    static final String TAG = "GlamourBt";
    public static final UUID SERVICE = UUID.fromString("7a3c2f5e-9b1d-4e8a-a6f2-3d5c8b9e1f42");
    final BluetoothAdapter adapter;
    final ConcurrentLinkedQueue<String> inbox = new ConcurrentLinkedQueue<>();
    final ExecutorService writer = Executors.newSingleThreadExecutor();
    volatile BluetoothServerSocket server;
    volatile BluetoothSocket socket;
    volatile OutputStream out;
    volatile boolean connected;
    volatile String status = "bereit";
    volatile String peer = "";
    volatile int gen;

    public GlamourBt() {
        BluetoothAdapter a = null;
        try { a = BluetoothAdapter.getDefaultAdapter(); } catch (Exception e) { Log.w(TAG, "kein Adapter: " + e); }
        adapter = a;
    }

    public boolean supported() { return adapter != null; }
    public boolean enabled() { try { return adapter != null && adapter.isEnabled(); } catch (Exception e) { return false; } }
    public boolean isConnected() { return connected; }
    public String status() { return status; }
    public String peer() { return peer; }

    /** Gekoppelte Geraete als "Name\tAdresse\n"-Liste. */
    public String paired() {
        StringBuilder sb = new StringBuilder();
        try {
            for (BluetoothDevice d : adapter.getBondedDevices()) {
                String n = d.getName(); sb.append(n == null ? d.getAddress() : n.replace('\t', ' ').replace('\n', ' ')).append('\t').append(d.getAddress()).append('\n');
            }
        } catch (SecurityException e) { status = "Berechtigung fehlt"; } catch (Exception e) { status = "Fehler: " + e.getMessage(); }
        return sb.toString();
    }

    /** Eroeffnen: Dienst anmelden und auf einen Mitspieler warten. */
    public void host() {
        stop(); final int g = ++gen;
        new Thread(() -> {
            try {
                status = "wartet auf Mitspieler";
                BluetoothServerSocket s = adapter.listenUsingRfcommWithServiceRecord("Glamour Games", SERVICE); server = s;
                BluetoothSocket c = s.accept(); try { s.close(); } catch (Exception ignored) { }
                if (g != gen) { c.close(); return; }
                begin(c, g);
            } catch (SecurityException e) { status = "Berechtigung fehlt"; }
            catch (Exception e) { if (g == gen) status = "Fehler: " + e.getMessage(); }
        }, "GlamourBt-host").start();
    }

    /** Beitreten: mit dem gekoppelten Geraet addr verbinden. */
    public void join(final String addr) {
        stop(); final int g = ++gen;
        new Thread(() -> {
            try {
                status = "verbindet ...";
                try { adapter.cancelDiscovery(); } catch (Exception ignored) { }
                BluetoothDevice d = adapter.getRemoteDevice(addr);
                BluetoothSocket c = d.createRfcommSocketToServiceRecord(SERVICE);
                c.connect();
                if (g != gen) { c.close(); return; }
                begin(c, g);
            } catch (SecurityException e) { status = "Berechtigung fehlt"; }
            catch (Exception e) { if (g == gen) status = "Verbindung fehlgeschlagen: " + e.getMessage(); }
        }, "GlamourBt-join").start();
    }

    void begin(BluetoothSocket c, int g) throws Exception {
        socket = c; out = c.getOutputStream();
        try { BluetoothDevice d = c.getRemoteDevice(); peer = d.getName() == null ? d.getAddress() : d.getName(); } catch (Exception e) { peer = "?"; }
        connected = true; status = "verbunden"; Log.i(TAG, "verbunden mit " + peer);
        BufferedReader r = new BufferedReader(new InputStreamReader(c.getInputStream(), StandardCharsets.UTF_8));
        try {
            String line;
            while ((line = r.readLine()) != null) inbox.add(line);
        } catch (Exception e) { Log.i(TAG, "Lesen beendet: " + e.getMessage()); }
        if (g == gen) { connected = false; status = "getrennt"; }
        try { c.close(); } catch (Exception ignored) { }
    }

    /** Eine Zeile senden (asynchron, Reihenfolge bleibt erhalten). */
    public void send(final String line) {
        final OutputStream o = out; if (o == null || !connected) return;
        writer.execute(() -> {
            try { o.write((line + "\n").getBytes(StandardCharsets.UTF_8)); o.flush(); }
            catch (Exception e) { connected = false; status = "getrennt"; }
        });
    }

    /** Naechste empfangene Zeile oder null. */
    public String poll() { return inbox.poll(); }

    public void stop() {
        gen++; connected = false; inbox.clear();
        try { if (server != null) server.close(); } catch (Exception ignored) { }
        try { if (socket != null) socket.close(); } catch (Exception ignored) { }
        server = null; socket = null; out = null; status = "bereit";
    }
}
