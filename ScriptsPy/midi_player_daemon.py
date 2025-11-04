import sys, os, json, time, threading, traceback
from mido import MidiFile, tick2second, bpm2tempo

# ========== util ==========
def println(obj):
    sys.stdout.write(json.dumps(obj, ensure_ascii=False) + "\n"); sys.stdout.flush()

class SafeFlag:
    def __init__(self):
        self._lock = threading.Lock(); self._val = False
    def set(self, v: bool):
        with self._lock: self._val = v
    def get(self) -> bool:
        with self._lock: return self._val

# ========== core ==========
class MidiDaemon:
    def __init__(self):
        self.fs = None
        self.sfid = None
        self.initialized = False
        self.play_thread = None
        self.playing_id = None
        self.stop_flag = SafeFlag()
        self._fluidsynth = None  # 遅延 import 後に格納

    def init(self, sf2: str, samplerate: int = 44100, driver: str = "dsound", gain: float = 0.5, dll_dir: str | None = None):
        # 1) DLL のディレクトリを検索パスに追加
        if dll_dir and os.path.isdir(dll_dir):
            try:
                # Windows 10+ 推奨の追加方法（PATHを書き換えずクリーン）
                os.add_dll_directory(dll_dir)
            except Exception as e:
                # 旧式フォールバック：PATH へ前置
                os.environ["PATH"] = dll_dir + os.pathsep + os.environ.get("PATH", "")
                println({"warn": "add_dll_directory failed -> PATH prepend", "detail": str(e), "dll_dir": dll_dir})

        # 2) ここで初めて fluidsynth を import（DLL が見える状態で）
        try:
            import fluidsynth  # type: ignore
            self._fluidsynth = fluidsynth
        except Exception as e:
            println({"error": "import fluidsynth failed", "detail": str(e), "dll_dir": dll_dir}); return

        # 3) SoundFont 確認
        if not os.path.isfile(sf2):
            println({"error": "sf2 not found", "path": sf2}); return

        # 4) FluidSynth 初期化
        try:
            fs = self._fluidsynth.Synth(samplerate=samplerate, gain=float(gain))
            fs.start(driver=driver)
            self.sfid = fs.sfload(sf2)
            # GM 初期化（ドラムは ch=9 bank=128）
            for ch in range(16):
                bank = 128 if ch == 9 else 0
                fs.program_select(ch, self.sfid, bank, 0)
            self.fs = fs
            self.initialized = True
            println({"ready": True, "sf2_loaded": True, "samplerate": samplerate, "driver": driver, "dll_dir": dll_dir})
        except Exception as e:
            println({"error": "init failed", "detail": str(e)})

    def _play_mid_worker(self, mid_path: str, req_id: str):
        try:
            mid = MidiFile(mid_path)
            tempo = bpm2tempo(120)
            tpq = mid.ticks_per_beat
            for msg in mid:
                if self.stop_flag.get() or self.playing_id != req_id:
                    break
                # mido の msg.time はファイルによって ticks/seconds の両方があり得るため堅めに処理
                delay = msg.time
                try:
                    # ticks と仮定して換算（秒が来た場合は小さな値になり安全に通る）
                    delay = tick2second(msg.time, tpq, tempo)
                except Exception:
                    pass
                if delay and delay > 0:
                    remain = float(delay)
                    step = 0.01
                    while remain > 0 and not self.stop_flag.get() and self.playing_id == req_id:
                        t = step if remain > step else remain
                        time.sleep(t); remain -= t
                if self.stop_flag.get() or self.playing_id != req_id:
                    break
                # メッセージ適用
                t = msg.type
                if t == 'set_tempo':
                    tempo = msg.tempo
                elif t == 'note_on':
                    self.fs.noteon(msg.channel, msg.note, msg.velocity)
                elif t == 'note_off':
                    self.fs.noteoff(msg.channel, msg.note)
                elif t == 'program_change':
                    bank = 128 if msg.channel == 9 else 0
                    self.fs.program_select(msg.channel, self.sfid, bank, msg.program)
                elif t == 'control_change':
                    self.fs.cc(msg.channel, msg.control, msg.value)
                elif t == 'pitchwheel':
                    self.fs.pitch_bend(msg.channel, msg.pitch)
            println({"stopped": req_id})
        except Exception as e:
            println({"error": "play_mid failed", "detail": str(e), "trace": traceback.format_exc(), "path": mid_path})
        finally:
            if self.playing_id == req_id:
                self.playing_id = None
            self.stop_flag.set(False)

    def play_mid(self, req_id: str, mid_path: str):
        if not self.initialized:
            println({"error": "not initialized"}); return
        if not os.path.isfile(mid_path):
            println({"error": "mid not found", "path": mid_path}); return
        # 単一ストリーム：既存停止→置換
        if self.play_thread and self.play_thread.is_alive():
            self.stop_flag.set(True)
            self.play_thread.join(timeout=1.0)
        self.stop_flag.set(False)
        self.playing_id = req_id
        self.play_thread = threading.Thread(target=self._play_mid_worker, args=(mid_path, req_id), daemon=True)
        self.play_thread.start()
        println({"playing": req_id, "path": mid_path})

    def note(self, ch: int, note: int, vel: int = 100):
        if not self.initialized:
            println({"error": "not initialized"}); return
        self.fs.noteon(int(ch), int(note), int(vel))
        println({"event": "note_on", "ch": int(ch), "note": int(note), "vel": int(vel)})

    def note_off(self, ch: int, note: int):
        if not self.initialized:
            println({"error": "not initialized"}); return
        self.fs.noteoff(int(ch), int(note))
        println({"event": "note_off", "ch": int(ch), "note": int(note)})

    def program(self, ch: int, program: int, drum: bool = False):
        if not self.initialized:
            println({"error": "not initialized"}); return
        bank = 128 if (bool(drum) or int(ch) == 9) else 0
        self.fs.program_select(int(ch), self.sfid, bank, int(program))
        println({"event": "program", "ch": int(ch), "program": int(program), "bank": bank})

    def shutdown(self):
        try:
            if self.play_thread and self.play_thread.is_alive():
                self.stop_flag.set(True)
                self.play_thread.join(timeout=1.0)
        finally:
            try:
                if self.fs is not None:
                    self.fs.delete()
            finally:
                println({"bye": True})

# ========== main ==========

def main():
    daemon = MidiDaemon()
    println({"hello": True, "api": ["init","play_mid","note","note_off","program","shutdown"]})
    for line in sys.stdin:
        if not line: break
        try:
            cmd = json.loads(line)
        except Exception as e:
            println({"error": "json", "detail": str(e)}); continue
        t = cmd.get("type")
        try:
            if t == "init":
                daemon.init(
                    sf2=cmd["sf2"],
                    samplerate=int(cmd.get("samplerate", 44100)),
                    driver=str(cmd.get("driver", "dsound")),
                    gain=float(cmd.get("gain", 0.5)),
                    dll_dir=cmd.get("dll_dir")
                )
            elif t == "play_mid":
                daemon.play_mid(cmd.get("id", "bgm"), cmd["path"])
            elif t == "note":
                daemon.note(int(cmd.get("ch", 0)), int(cmd["note"]), int(cmd.get("vel", 100)))
            elif t == "note_off":
                daemon.note_off(int(cmd.get("ch", 0)), int(cmd["note"]))
            elif t == "program":
                daemon.program(int(cmd.get("ch", 0)), int(cmd.get("program", 0)), bool(cmd.get("drum", False)))
            elif t == "shutdown":
                daemon.shutdown(); break
            else:
                println({"error": "unknown cmd", "cmd": cmd})
        except KeyError as e:
            println({"error": "missing field", "detail": str(e), "cmd": cmd})
        except Exception as e:
            println({"error": "exception", "detail": str(e), "cmd": cmd, "trace": traceback.format_exc()})

if __name__ == "__main__":
    main()