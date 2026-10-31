# SummonCommander (następca SkeletonCommander)

Mod obsługuje przyzwańce z Dead Raiser (`Skeleton_Friendly`) i Spirit Caller (`*_spiritcaller`: Wolf, Boar,
Moose, Bjorn). Typ rozpoznaje `Summons.GetKind`.
Spirit Caller losuje gatunek przy każdym rzucie; limit jest osobny dla każdego gatunku (N sztuk każdego przy
poziomie laski N), dlatego limity liczone są per `m_name` (`Summons.LimitGroups`), tak jak w `Tameable.UnsummonMaxInstances`.

## Komendy
- Budowanie i deploy: `dotnet build`
- Czyszczenie: `dotnet clean`
- Dekompilacja klas gry: `ilspycmd -t <NazwaKlasy> ./lib/assembly_valheim.dll`

## Środowisko i Ścieżki
- Środowisko: Ubuntu (Linux), .NET SDK 10 (Target Framework: `netstandard2.1`)
- Referencje gier (DLL): `./lib/`
- Ścieżka BepInEx Plugins: `/home/michal/snap/steam/common/.steam/steam/steamapps/common/Valheim/BepInEx/plugins/`

## Zasady i Konwencje
- Wykorzystuj BepInEx 5 oraz Harmony.
- Plik `.csproj` zawiera MSBuild Target (`AfterTargets="Build"`), który po `dotnet build` kopiuje plik `.dll` do BepInEx/plugins
- W przypadku braku pewności co do metod lub zmiennych z Valheim, najpierw wykonaj `ilspycmd` na odpowiedniej klasie z `./lib/assembly_valheim.dll`.
- Przy każdej zmianie w modzie podnoś wersję `PluginVersion` w `SummonCommanderPlugin.cs` (semver: poprawki → patch, nowe funkcje → minor, zmiany łamiące kompatybilność → major), chyba że bieżąca wersja nie została jeszcze wydana — wtedy dopisuj do niej. Każdą zmianę opisz w `CHANGELOG.md` pod tą wersją (po angielsku).
- Mod ma pierwszeństwo przed SkeletonCommander: SkeletonCommander 1.10.0+ ma `BepInIncompatibility` z tym modem i się nie ładuje,
  a starsze wersje (miękka zależność → ładują się wcześniej) są wyłączane w `DisableSkeletonCommander`.
- Teksty widoczne dla gracza (HUD, komunikaty, czat, piny) idą przez `Texts.Get`/`Texts.Format` z angielskim tekstem jako kluczem; każdy nowy tekst dopisz też do polskiej tabeli w `Texts.cs`. Komendy `/sc` i opisy configu zostają po angielsku.
- Autor moda: xMordax.
