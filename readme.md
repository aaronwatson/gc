# General Conference of The Church of Jesus Christ of Latter-day Saints
## App downloads mp3 audio files of all talks from the specified conference
### Each MP3 file will include
- Title
- Speaker
- Image of speaker
- Text of talk set as embedded lyrics

## Build the app
```
dotnet build
```

## Run the app
```
dotnet run -y [year of conference] -m [month (usually 04 or 10)] -o [folder to save mp3 talks]
```

e.g.
```
dotnet run -y 2024 -m 04 -o 2024-april-conference
```
