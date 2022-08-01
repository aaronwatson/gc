# General Conference of The Church of Jesus Christ of Latter-day Saints
## App downloads mp3 audio files of all talks from the most recent conference

**To build the app:**
`docker build -f dockerfile . -t gc`

**To run the app:**
`docker run gc -d /opt/selenium/ -o /tmp`