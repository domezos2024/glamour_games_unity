package de.domezosware.glamourgames;

import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothClass;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothServerSocket;
import android.bluetooth.BluetoothSocket;
import android.os.ParcelUuid;
import android.util.Log;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Bluetooth-Mehrverbindung (RFCOMM/SPP) fuer Glamour Games. Als Host nimmt das Plugin bis zu maxPeers Gaeste an
 * (Accept-Schleife, bleibt auch im Spiel offen fuer Wiederverbindung); als Gast besteht genau eine Verbindung (Peer 0).
 * Jede Verbindung hat einen eigenen Lese-Thread und eine eigene Schreibwarteschlange. Ereignisse kommen als Zeilen ueber poll():
 *   C|peer|name|adresse        verbunden
 *   L|peer|zeile               Nachricht
 *   X|peer|fehlercode|text     Verbindung beendet (Codes wie BtErr in C#)
 * Peer-Nummern des Hosts zaehlen ab 1 und werden nicht wiederverwendet. Gleiche Dienst-UUID wie die Windows-Version.
 */
public class GlamourBt {
    static final String TAG = "GlamourBt";
    public static final UUID SERVICE = UUID.fromString("7a3c2f5e-9b1d-4e8a-a6f2-3d5c8b9e1f42");
    // Fehlercodes, identisch zu BtErr in Transport.cs
    static final int E_PERM = 1, E_OFF = 2, E_NOADAPTER = 3, E_CONNECT = 4, E_TIMEOUT = 5, E_CLOSED = 6, E_READ = 7, E_WRITE = 8, E_FULL = 9, E_UNKNOWN = 10;

    final BluetoothAdapter adapter;
    final ConcurrentLinkedQueue<String> events = new ConcurrentLinkedQueue<>();
    final ConcurrentHashMap<Integer, Conn> conns = new ConcurrentHashMap<>();
    final AtomicInteger nextPeer = new AtomicInteger(1);
    volatile BluetoothServerSocket server;
    volatile String status = "bereit";
    volatile int lastError = 0;
    volatile int gen;
    volatile int maxPeers = 3;

    /** Eine Verbindung mit eigener Schreibwarteschlange. */
    final class Conn {
        final int id; final BluetoothSocket sock; final OutputStream out; final ExecutorService writer = Executors.newSingleThreadExecutor();
        final int g; volatile boolean closed; volatile boolean reported;
        Conn(int id, BluetoothSocket s, int g) throws Exception { this.id = id; this.sock = s; this.out = s.getOutputStream(); this.g = g; }
        void send(final String line) {
            if (closed) return;
            writer.execute(() -> {
                if (closed) return;
                try { out.write((line + "\n").getBytes(StandardCharsets.UTF_8)); out.flush(); }
                catch (Exception e) { finish(this, E_WRITE, "Schreibfehler: " + e.getMessage()); }
            });
        }
        /** Erst die Sendewarteschlange leeren, dann schliessen (z. B. ERROR/BYE kommt noch beim Gegenueber an). */
        void closeGraceful() {
            try { writer.execute(() -> hardClose()); writer.shutdown(); } catch (Exception e) { hardClose(); }
        }
        void hardClose() { closed = true; try { sock.close(); } catch (Exception ignored) { } try { writer.shutdownNow(); } catch (Exception ignored) { } }
    }

    public GlamourBt() {
        BluetoothAdapter a = null;
        try { a = BluetoothAdapter.getDefaultAdapter(); } catch (Exception e) { Log.w(TAG, "kein Adapter: " + e); }
        adapter = a;
    }

    public boolean supported() { return adapter != null; }
    public boolean enabled() { try { return adapter != null && adapter.isEnabled(); } catch (Exception e) { return false; } }
    public boolean listening() { return server != null; }
    public int connectionCount() { int n = 0; for (Conn c : conns.values()) if (!c.closed) n++; return n; }
    public String status() { return status; }
    public int lastError() { return lastError; }
    public void setMaxPeers(int n) { maxPeers = Math.max(1, Math.min(7, n)); }

    /** Gekoppelte Geraete als "Name\tAdresse\tHauptklasse\tGlamour(0/1)\n"-Liste; Glamour = Dienst-UUID in der zuletzt bekannten Dienstliste. */
    public String paired() {
        StringBuilder sb = new StringBuilder();
        try {
            for (BluetoothDevice d : adapter.getBondedDevices()) {
                String n = d.getName(); int major = 0;
                try { BluetoothClass c = d.getBluetoothClass(); if (c != null) major = c.getMajorDeviceClass(); } catch (Exception ignored) { }
                sb.append(n == null ? d.getAddress() : n.replace('\t', ' ').replace('\n', ' ')).append('\t').append(d.getAddress())
                  .append('\t').append(major).append('\t').append(offers(d) ? '1' : '0').append('\n');
            }
        } catch (SecurityException e) { status = "Berechtigung fehlt"; lastError = E_PERM; } catch (Exception e) { status = "Fehler: " + e.getMessage(); }
        return sb.toString();
    }

    static boolean offers(BluetoothDevice d) {
        try { ParcelUuid[] u = d.getUuids(); if (u != null) for (ParcelUuid p : u) if (SERVICE.equals(p.getUuid())) return true; } catch (Exception ignored) { }
        return false;
    }

    /** Dienstlisten gekoppelter PCs/Handys im Hintergrund neu abfragen (Ergebnis erscheint beim naechsten paired()). */
    public void scan() {
        try {
            for (BluetoothDevice d : adapter.getBondedDevices()) {
                BluetoothClass c = d.getBluetoothClass(); int m = c == null ? 0 : c.getMajorDeviceClass();
                if (m == BluetoothClass.Device.Major.COMPUTER || m == BluetoothClass.Device.Major.PHONE) d.fetchUuidsWithSdp();
            }
        } catch (Exception e) { Log.w(TAG, "scan: " + e); }
    }

    /** Bluetooth-Name dieses Geraets. */
    public String localName() { try { return adapter == null ? "" : adapter.getName(); } catch (Exception e) { return ""; } }

    /** Host: Dienst anmelden und Gaeste annehmen, solange die Sitzung offen ist (Accept-Schleife). */
    public void startHost() {
        stop(); final int g = ++gen; lastError = 0;
        new Thread(() -> {
            BluetoothServerSocket s = null;
            try {
                status = "wartet auf Mitspieler";
                s = adapter.listenUsingRfcommWithServiceRecord("Glamour Games", SERVICE); server = s;
                while (g == gen) {
                    BluetoothSocket c = s.accept();
                    if (g != gen) { try { c.close(); } catch (Exception ignored) { } break; }
                    if (connectionCount() >= maxPeers) { try { c.close(); } catch (Exception ignored) { } continue; } // voll: Rest regelt die Sitzungsschicht nicht, hier hart ablehnen
                    try { attach(c, g, nextPeer.getAndIncrement()); } catch (Exception e) { Log.w(TAG, "attach: " + e); try { c.close(); } catch (Exception ignored) { } }
                }
            } catch (SecurityException e) { if (g == gen) { status = "Berechtigung fehlt"; lastError = E_PERM; events.add("X|0|" + E_PERM + "|Berechtigung fehlt"); } }
            catch (Exception e) { if (g == gen) { status = "Fehler: " + e.getMessage(); lastError = enabled() ? E_UNKNOWN : E_OFF; events.add("X|0|" + lastError + "|" + clean(e.getMessage())); } }
            finally { if (g == gen) server = null; try { if (s != null) s.close(); } catch (Exception ignored) { } }
        }, "GlamourBt-host").start();
    }

    /** Gast: mit dem gekoppelten Geraet addr verbinden (Peer 0). */
    public void connect(final String addr) {
        stop(); final int g = ++gen; lastError = 0;
        new Thread(() -> {
            try {
                status = "verbindet ...";
                try { adapter.cancelDiscovery(); } catch (Exception ignored) { }
                BluetoothDevice d = adapter.getRemoteDevice(addr);
                BluetoothSocket c = d.createRfcommSocketToServiceRecord(SERVICE);
                c.connect();
                if (g != gen) { c.close(); return; }
                attach(c, g, 0);
            } catch (SecurityException e) { if (g == gen) { status = "Berechtigung fehlt"; lastError = E_PERM; events.add("X|0|" + E_PERM + "|Berechtigung fehlt"); } }
            catch (Exception e) { if (g == gen) { status = "Verbindung fehlgeschlagen: " + e.getMessage(); lastError = enabled() ? E_CONNECT : E_OFF; events.add("X|0|" + lastError + "|" + clean(e.getMessage())); } }
        }, "GlamourBt-join").start();
    }

    void attach(BluetoothSocket c, final int g, final int id) throws Exception {
        final Conn cn = new Conn(id, c, g); conns.put(id, cn);
        String name = "?", addr = "";
        try { BluetoothDevice d = c.getRemoteDevice(); addr = d.getAddress(); name = d.getName() == null ? addr : d.getName(); } catch (Exception ignored) { }
        status = "verbunden"; Log.i(TAG, "verbunden mit " + name + " als Peer " + id);
        events.add("C|" + id + "|" + clean(name) + "|" + addr);
        final BufferedReader r = new BufferedReader(new InputStreamReader(c.getInputStream(), StandardCharsets.UTF_8), 8192);
        Thread t = new Thread(() -> {
            int code = E_CLOSED; String txt = "";
            try {
                String line;
                while (!cn.closed && (line = r.readLine()) != null) {
                    if (line.length() > 40000) { code = E_READ; txt = "Zeile zu lang"; break; }
                    events.add("L|" + id + "|" + line);
                }
            } catch (Exception e) { code = cn.closed ? E_CLOSED : E_READ; txt = clean(e.getMessage()); }
            finish(cn, code, txt);
        }, "GlamourBt-read-" + id);
        t.setDaemon(true); t.start();
    }

    /** Verbindung genau einmal als beendet melden und aufraeumen. */
    synchronized void finish(Conn c, int code, String txt) {
        if (c.reported) return; c.reported = true; c.hardClose(); conns.remove(c.id, c);
        if (c.g == gen) { events.add("X|" + c.id + "|" + code + "|" + clean(txt)); if (connectionCount() == 0 && server == null) status = "getrennt"; }
    }

    static String clean(String s) { return s == null ? "" : s.replace('\n', ' ').replace('\r', ' '); }

    /** Eine Zeile an Peer senden (asynchron, Reihenfolge je Peer bleibt erhalten). */
    public void send(int peer, String line) { Conn c = conns.get(peer); if (c != null) c.send(line); }

    /** Verbindung ordentlich beenden (Sendewarteschlange wird vorher geleert). Meldet danach ein X-Ereignis. */
    public void close(int peer) {
        Conn c = conns.get(peer); if (c == null) return;
        c.closeGraceful();
        // Lese-Thread erkennt das Ende und meldet X; falls der Socket haengt, nach kurzer Zeit hart beenden
        new Thread(() -> { try { Thread.sleep(1500); } catch (Exception ignored) { } finish(c, E_CLOSED, ""); }).start();
    }

    /** Naechstes Ereignis oder null. */
    public String poll() { return events.poll(); }

    public void stop() {
        gen++;
        try { if (server != null) server.close(); } catch (Exception ignored) { }
        for (Conn c : conns.values()) c.hardClose();
        conns.clear(); events.clear(); nextPeer.set(1); server = null; status = "bereit"; lastError = 0;
    }
}
