# cleanuperr

## Usage

```
docker run \
    -e QuartzConfig__BlockedTorrentTrigger="0 0/10 * * * ?" \
    -e QBitConfig__Url="http://localhost:8080" \
    -e QBitConfig__Username="user" \
    -e QBitConfig__Password="pass" \
    -e SonarrConfig__Intances__0__Url="http://localhost:8989" \
    -e SonarrConfig__Intances__0__ApiKey="secret1" \
    -e SonarrConfig__Intances__1__Url="http://localhost:8990" \
    -e SonarrConfig__Intances__1__ApiKey="secret2" \
    ...
    flaminel/cleanuperr:latest
```

## Environment variables

| Variable | Required | Description | Default value |
|---|---|---|---|
| QuartzConfig__BlockedTorrentTrigger | No | Quartz cron trigger | 0 0/5 * * * ? |
| QBitConfig__Url | Yes | qBittorrent instance url | http://localhost:8080 |
| QBitConfig__Username | Yes | qBittorrent user | empty |
| QBitConfig__Password | Yes | qBittorrent password | empty |
| SonarrConfig__Intances__0__Url | Yes | First Sonarr instance url | http://localhost:8989 |
| SonarrConfig__Intances__0__ApiKey | Yes | First Sonarr instance API key | empty |

#

Multiple Sonarr instances can be specified using this format:

```
SonarrConfig__Intances__<NUMBER>__Url
SonarrConfig__Intances__<NUMBER>__ApiKey
```

where `<NUMBER>` starts from 0.

## How it works

1. Add excluded file names to prevent malicious files from being downloaded by qBittorrent.
2. cleanuperr goes through all items in Sonarr's queue every at every 5th minute.
3. For each queue item, a call is made to qBittorrent to get the stats of the torrent.
4. If a torrent is found to be marked as completed, but with 0 downloaded bytes, cleanuperr calls Sonarr to add that torrent to the blocklist.
5. If any malicious torrents have been found, cleanuperr calls Sonarr to automatically search again.

<details> 
    <summary>Extensions to block</summary>
    <pre><code>*.apk
*.bat
*.bin
*.bmp
*.cmd
*.com
*.db
*.diz
*.dll
*.dmg
*.etc
*.exe
*.gif
*.htm
*.html
*.ico
*.ini
*.iso
*.jar
*.jpg
*.js
*.link
*.lnk
*.msi
*.nfo
*.perl
*.php
*.pl
*.png
*.ps1
*.psc1
*.psd1
*.psm1
*.py
*.pyd
*.rb
*.readme
*.reg
*.run
*.scr
*.sh
*.sql
*.text
*.thumb
*.torrent
*.txt
*.url
*.vbs
*.wsf
*.xml
*.zipx
*.7z
*.bdjo
*.bdmv
*.bin
*.bmp
*.cci
*.clpi
*.crt
*.dll
*.exe
*.html
*.idx
*.inf
*.jar
*.jpeg
*.jpg
*.lnk
*.m4a
*.mpls
*.msi
*.nfo
*.pdf
*.png
*.rar
*(sample).*
*sample.mkv
*sample.mp4
*.sfv
*.srt
*.sub
*.tbl
Trailer.*
*.txt
*.url
*.xig
*.xml
*.xrt
*.zip
*.zipx
*.Lnk
</code></pre>
</details>