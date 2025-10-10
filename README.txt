- 📘 [Spécification (SPEC)](docs/SPEC.md)
- ✅ [Décisions actées](docs/DECISIONS.md)
- 🗺️ [Roadmap / TODO](docs/TODO.md)

VorTech.App — WPF .NET 8 (squelette complet)

Commandes:
  dotnet build
  .\bin\Debug\net8.0-windows\win-x64\VorTech.App.exe

Publication:
  dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false -o .\publish

Le fichier Data\app.db est créé au premier lancement, avec des données démo.
