import contextlib
import io
import runpy
import sys, os, json, time, threading, traceback
from mido import MidiFile

# ========== util ==========
def println(obj):
    sys.stdout.write(json.dumps(obj, ensure_ascii=False) + "\n"); sys.stdout.flush()


SCRIPT_ROOT = os.path.dirname(os.path.abspath(__file__))


def run_script(request_id: str, script_path: str, args=None, stdin_text: str = ""):
    """Run a ScriptsPy CLI entry point inside this persistent interpreter."""
    args = [str(value) for value in (args or [])]
    script_path = os.path.abspath(script_path)
    stdout_buffer = io.StringIO()
    stderr_buffer = io.StringIO()
    exit_code = 0

    try:
        if os.path.commonpath([SCRIPT_ROOT, script_path]) != SCRIPT_ROOT:
            raise ValueError(f"script must be under ScriptsPy: {script_path}")
        if not os.path.isfile(script_path):
            raise FileNotFoundError(script_path)

        old_argv = sys.argv
        old_stdin = sys.stdin
        old_cwd = os.getcwd()
        try:
            sys.argv = [script_path, *args]
            sys.stdin = io.StringIO(stdin_text or "")
            os.chdir(os.path.dirname(script_path))
            with contextlib.redirect_stdout(stdout_buffer), contextlib.redirect_stderr(stderr_buffer):
                try:
                    runpy.run_path(script_path, run_name="__main__")
                except SystemExit as exc:
                    if exc.code is None:
                        exit_code = 0
                    elif isinstance(exc.code, int):
                        exit_code = exc.code
                    else:
                        exit_code = 1
                        print(exc.code, file=sys.stderr)
        finally:
            sys.argv = old_argv
            sys.stdin = old_stdin
            os.chdir(old_cwd)
    except Exception:
        exit_code = 1
        traceback.print_exc(file=stderr_buffer)

    println({
        "event": "script_result",
        "request_id": request_id,
        "exit_code": exit_code,
        "stdout": stdout_buffer.getvalue(),
        "stderr": stderr_buffer.getvalue(),
    })

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
        self._dll_directory_handles = []

    def init(self, sf2: str, samplerate: int = 44100, driver: str = "dsound", gain: float = 0.5, dll_dir: str | None = None):
        self.initialized = False

        # 1) Unityから渡されたDLLディレクトリを検索パスに追加し、
        #    ハンドルをデーモン終了まで保持する。
        if not dll_dir or not os.path.isdir(dll_dir):
            println({"error": "init failed", "detail": "FluidSynth DLL directory not found", "dll_dir": dll_dir}); return

        os.environ["PATH"] = dll_dir + os.pathsep + os.environ.get("PATH", "")
        original_add_dll_directory = getattr(os, "add_dll_directory", None)
        if original_add_dll_directory is not None:
            try:
                handle = original_add_dll_directory(dll_dir)
                if handle is not None:
                    self._dll_directory_handles.append(handle)
            except Exception as e:
                println({"warn": "add_dll_directory failed; using PATH", "detail": str(e), "dll_dir": dll_dir})

        # 2) pyFluidSynth 1.3.4は存在しない固定パス
        #    C:\tools\fluidsynth\bin も登録しようとするため、import中だけ
        #    存在するディレクトリに限定してadd_dll_directoryを実行する。
        def add_existing_dll_directory(path):
            if not path or not os.path.isdir(path):
                return None
            handle = original_add_dll_directory(path)
            if handle is not None:
                self._dll_directory_handles.append(handle)
            return handle

        if original_add_dll_directory is not None:
            os.add_dll_directory = add_existing_dll_directory

        try:
            import fluidsynth  # type: ignore
            self._fluidsynth = fluidsynth
        except Exception as e:
            println({"error": "import fluidsynth failed", "detail": str(e), "dll_dir": dll_dir}); return
        finally:
            if original_add_dll_directory is not None:
                os.add_dll_directory = original_add_dll_directory

        # 3) SoundFont 確認
        if not os.path.isfile(sf2):
            println({"error": "sf2 not found", "path": sf2}); return

        # 4) FluidSynth 初期化
        try:
            fs = self._fluidsynth.Synth(samplerate=samplerate, gain=float(gain))
            fs.start(driver=driver)
            self.sfid = fs.sfload(sf2)
            if self.sfid < 0:
                raise RuntimeError(f"Failed to load SoundFont: {sf2}")
            # GM 初期化（ドラムは ch=9 bank=128）
            for ch in range(16):
                bank = 128 if ch == 9 else 0
                fs.program_select(ch, self.sfid, bank, 0)
            self.fs = fs
            self.initialized = True
            println({"ready": True, "sf2_loaded": True, "samplerate": samplerate, "driver": driver, "dll_dir": dll_dir})
        except Exception as e:
            self.initialized = False
            try:
                if 'fs' in locals():
                    fs.delete()
            except Exception:
                pass
            println({"error": "init failed", "detail": str(e)})

    def _play_mid_worker(self, mid_path: str, req_id: str):
        try:
            mid = MidiFile(mid_path)
            for msg in mid:
                if self.stop_flag.get() or self.playing_id != req_id:
                    break
                # MidiFileを直接反復した場合、msg.timeはテンポを反映済みの秒単位。
                # tickとして再変換すると音符間隔がほぼ0になり、和音のように聞こえる。
                delay = max(0.0, float(msg.time))
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
                    continue
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

    def stop_mid(self):
        if self.play_thread and self.play_thread.is_alive():
            self.stop_flag.set(True)
            self.play_thread.join(timeout=1.0)
        self.playing_id = None
        self.stop_flag.set(False)
        println({"stopped": True})

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
                self.initialized = False
                self._dll_directory_handles.clear()
                println({"bye": True})

# ========== main ==========

def main():
    daemon = MidiDaemon()
    println({"hello": True, "api": ["init","run_script","play_mid","stop_mid","note","note_off","program","shutdown"]})
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
            elif t == "run_script":
                run_script(
                    request_id=str(cmd["request_id"]),
                    script_path=str(cmd["script"]),
                    args=cmd.get("args", []),
                    stdin_text=str(cmd.get("stdin", "")),
                )
            elif t == "play_mid":
                daemon.play_mid(cmd.get("id", "bgm"), cmd["path"])
            elif t == "stop_mid":
                daemon.stop_mid()
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
