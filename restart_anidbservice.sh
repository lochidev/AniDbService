#! /bin/bash
mkdir -p mystuff/AniDbService/publish
cd mystuff/AniDbService/publish
PROCESS=$(bash $HOME/get_process.sh [.]/AniDbService)
kill -9 $PROCESS
printf "Killed AniDbService with pId $PROCESS\n"
printf "Enter Redis Connection String\n"
read RCS
export Redis=$RCS
printf "Enter AniDb Username\n"
read AUSR
export AniDb_User=$AUSR
printf "Enter AniDb Password\n"
read APW
export AniDb_Password=$APW
printf "Enter AniDb UDP Client\n"
read AUDP
export AniDb_UDPClient=$AUDP
printf "Enter AniDb HTTP Client\n"
read AHTTP
export AniDb_HTTPClient=$AHTTP
printf "Enter AniDb Client Version\n"
read ACW
export AniDb_ClientVersion=$ACW
nohup ./AniDbService urls=https://localhost:5015 >/dev/null 2>&1 &
ps aux | grep dotnet