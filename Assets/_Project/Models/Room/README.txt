📷 Tu wrzuć model pokoju z Polycam

OCZEKIWANY PLIK:
  TrainingRoom.glb  (lub .fbx)

INSTRUKCJA POLYCAM:
1. Pobierz aplikację Polycam (iOS App Store / Google Play / poly.cam)
2. Wybierz tryb: Room Mode (LiDAR) lub Photo Mode (40-80 zdjęć)
3. Skanuj swój pokój:
   - Wszystkie światła włączone
   - Stań w środku, powoli obróć się o 360°
   - Podejdź do każdego mebla używanego w grze:
     • Krzesło (siedzenie w Akcie II - poślizg)
     • Biurko (na nim telefon w Akcie III)
     • Szafa (ukrycie w Akcie III)
     • Wolna podłoga (Remy upada w Akcie I)
4. Po przetworzeniu: Edit → przytnij niepotrzebne fragmenty
5. Export → format .glb (zalecane) → zapisz tu jako TrainingRoom.glb
6. Drag & drop do Unity Editor → automatyczny import

PO IMPORCIE W UNITY:
- W scenie TrainingRoom.unity: dodaj prefab modelu jako child of "Room"
- Na komponencie RoomEnvironment.cs przypisz Transform anchorów:
  • Victim Spawn Anchor → środek wolnej podłogi
  • Chair Anchor → krzesło
  • Desk Anchor → biurko
  • Phone Anchor → na biurku
  • Wardrobe Anchor → szafa
  • Player Start Anchor → gdzie ma stać gracz na początku

OPTYMALIZACJA (jeśli model jest ciężki dla Quest 3):
- W importerze: Read/Write Enabled → OFF
- Mesh Compression → Medium
- Generate Lightmap UVs → ON
- Materials → Convert to URP (gdy prompt)
