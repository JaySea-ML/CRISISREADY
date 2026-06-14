# Instalacja, budowanie i uruchomienie — CRISISREADY

Gra treningowa Mixed Reality na **Meta Quest 3 / 3S**. Silnik: **Unity 6 LTS (6000.0.74f1)**, URP, OpenXR, IL2CPP / ARM64. Pakiet: `com.grupa1.mrcrisistrainer`. Sterowanie wyłącznie śledzeniem dłoni (bez kontrolerów) + mikrofon, z passthrough (prawdziwe MR).

Poniższa instrukcja prowadzi krok po kroku od pustego środowiska do działającej gry na goglach.

---

## 1. Wymagania

### Oprogramowanie

- **Unity Hub** — menedżer instalacji Unity ([unity.com/download](https://unity.com/download)).
- **Unity 6000.0.74f1** — dokładnie ta wersja (LTS). Inne wersje mogą powodować błędy importu i niezgodność pakietów.
  - Najprościej zainstalować przez Unity Hub: zakładka *Installs* → *Install Editor* → *Archive* / *Download Archive* → wybierz `6000.0.74f1`.
- **Moduł Android Build Support** — podczas instalacji edytora zaznacz:
  - **Android Build Support**
  - **OpenJDK**
  - **Android SDK & NDK Tools**
  - (te dwa są wymagane do budowy APK i do kompilacji **IL2CPP** dla ARM64)
- **IL2CPP** — wbudowane w moduł Android, dodatkowa instalacja nie jest potrzebna; backend jest już ustawiony w projekcie.

### Sprzęt i konfiguracja gogli

- **Meta Quest 3** lub **Meta Quest 3S** w **trybie deweloperskim** (developer mode).
  - Tryb deweloperski włącza się w aplikacji **Meta Horizon** na telefonie: *Menu → Urządzenia → wybierz gogle → Ustawienia dewelopera → włącz Developer Mode*. Wymaga konta dewelopera Meta (założenie konta jest darmowe).
  - Po włączeniu trybu na goglach pojawi się monit *Allow USB debugging* — należy go zaakceptować (warto zaznaczyć *Always allow from this computer*).
- **ADB (Android Debug Bridge)** — narzędzie do wgrywania APK przez USB.
  - ADB jest dostarczane razem z Android SDK instalowanym przez Unity (`<ścieżka Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe`).
  - Można też zainstalować osobno przez Android Platform Tools lub Meta Quest Developer Hub. Sprawdź, czy działa:
    ```
    adb version
    ```
- **Kabel USB-C** (data, nie tylko ładowanie) do połączenia gogli z komputerem.

---

## 2. Otwarcie projektu

1. Uruchom **Unity Hub**.
2. Przejdź do zakładki **Projects** → kliknij **Add** → **Add project from disk**.
3. Wskaż folder główny projektu (ten, w którym znajdują się katalogi `Assets/`, `ProjectSettings/`, `Packages/`).
4. Na liście projektów kliknij projekt — upewnij się, że obok widnieje wersja **6000.0.74f1** (Hub zaproponuje doinstalowanie, jeśli jej brak).
5. Otwórz projekt. **Pierwsze otwarcie potrwa kilka–kilkanaście minut** — Unity odbuduje katalog `Library/` (import wszystkich assetów, kompilacja skryptów, pobranie pakietów). To normalne; nie przerywaj procesu.

> Katalog `Library/` jest generowany lokalnie i nie powinien być wersjonowany — po sklonowaniu repo zawsze powstaje od nowa.

---

## 3. Przebudowa sceny gry

**Cała scena gry budowana jest z kodu** (skrypty `ActsBuilder` / `LabRoomBuilder` / `MetaSceneBuilder`). Po każdej edycji buildera scenę trzeba odbudować, aby zmiany trafiły do pliku sceny.

W menu Unity wybierz:

```
MRCrisis → Build Lab Room Scene
```

Polecenie tworzy / nadpisuje scenę gry wraz z aktami (Akt II „Poślizg", Akt III „Intruz") i treningiem. Po zakończeniu zapisz projekt (`Ctrl+S`). Dopóki nie przebudujesz sceny, build APK będzie zawierał poprzednią wersję.

---

## 4. Budowa APK

Dwie równoważne metody — obie produkują `Builds/MRCrisisTrainer.apk` (IL2CPP, ARM64):

**A. Z menu (zalecane):**

```
MRCrisis → Build APK (Quest 3)
```

**B. Z kodu / wsadowo** — wywołaj statyczną metodę:

```
MRCrisisTrainer.EditorTools.BuildScript.BuildAndroidApk()
```

Tej samej metody można użyć w trybie wsadowym (bez otwierania edytora):

```
Unity.exe -batchmode -quit -projectPath "<ścieżka_projektu>" ^
  -executeMethod MRCrisisTrainer.EditorTools.BuildScript.BuildAndroidApk
```

Budowa potrwa kilka minut (kompilacja IL2CPP jest wolniejsza niż Mono). Gotowy plik pojawi się w katalogu **`Builds/MRCrisisTrainer.apk`**.

---

## 5. Wgranie na gogle

1. Podłącz gogle kablem USB-C, załóż je i zaakceptuj ewentualny monit *Allow USB debugging*.
2. Sprawdź, czy ADB widzi urządzenie:
   ```
   adb devices
   ```
   (urządzenie powinno być na liście jako `device`, nie `unauthorized`).
3. Zainstaluj / zaktualizuj aplikację (flaga `-r` nadpisuje poprzednią instalację bez utraty danych):
   ```
   adb install -r Builds/MRCrisisTrainer.apk
   ```
4. **Tryb developer** musi być włączony w aplikacji **Meta Horizon** (patrz sekcja *Wymagania*) — bez niego instalacja przez ADB zostanie odrzucona.
5. Na goglach uruchom aplikację: **Biblioteka → Nieznane źródła (Unknown Sources) → MRCrisisTrainer**.

---

## 6. Gotowy build

Jeśli nie chcesz budować samodzielnie — **gotowe APK jest już w repozytorium**:

```
Builds/MRCrisisTrainer.apk
```

Wystarczy wykonać krok *Wgranie na gogle*:

```
adb install -r Builds/MRCrisisTrainer.apk
```

---

## 7. Najczęstsze problemy

### Brak śledzenia dłoni (hand tracking)
Gra sterowana jest **wyłącznie dłońmi** — bez działającego hand-trackingu nie zagrasz.
- Włącz śledzenie dłoni w goglach: *Ustawienia → Ruch i śledzenie → Śledzenie dłoni* (zalecane *Auto* przełączanie ręce/kontrolery).
- Zadbaj o dobre oświetlenie i odsłonięte dłonie (rękawiczki / ciemność psują tracking).
- Jeśli mimo to nie działa — zdejmij i ponownie załóż gogle, aby zresetować sesję śledzenia.

### Różowe / magenta materiały (pink/magenta)
Różowe powierzchnie oznaczają **materiały niezgodne z URP** (shader nie został zmigrowany do Universal Render Pipeline).
- Upewnij się, że projekt został w pełni zaimportowany (kompletny `Library/`).
- Jeśli materiały nadal są różowe, użyj w edytorze: *Edit → Rendering → Materials → Convert All Built-in Materials to URP*, lub uruchom konfigurację potoku z menu `MRCrisis → Setup URP Pipeline`.
- Różowy tekst (TextMesh Pro) wskazuje brak zasobów TMP Essentials — zaimportuj je przez *Window → TextMeshPro → Import TMP Essential Resources*.

### `ZombieHitman.fbx` — model wykluczony
Plik `Assets/_Project/External/Act3/ZombieHitman/ZombieHitman.fbx` jest **celowo nieużywany** w grze (zastąpiony innym assetem postaci). To zamierzone — nie jest błędem i nie trzeba go importować ani podpinać do sceny. Jeśli import tego FBX zgłasza ostrzeżenia, można je zignorować.

---

## Skrót — szybka ścieżka

```
# 1. Otwórz projekt w Unity Hub (Add → wybierz folder), poczekaj na Library
# 2. (opcjonalnie) Odbuduj scenę:   MRCrisis → Build Lab Room Scene
# 3. (opcjonalnie) Zbuduj APK:       MRCrisis → Build APK (Quest 3)
# 4. Wgraj gotowe APK na gogle:
adb install -r Builds/MRCrisisTrainer.apk
```
