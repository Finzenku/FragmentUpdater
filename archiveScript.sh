if [ -d "builds" ]; then rm -Rf builds; fi
mkdir builds

cd builds

mkdir linux-x64
mkdir win-x64
mkdir osx-x64

cp -a ../bin/Release/net5.0/win-x64/publish/ win-x64/
cp -a ../bin/Release/net5.0/linux-x64/publish/ linux-x64/
cp -a ../bin/Release/net5.0/osx-x64/publish/ osx-x64/

zip -r -j fragment-updater-win-x64.zip win-x64/*
zip -r -j fragment-updater-linux-x64.zip linux-x64/*
zip -r -j fragment-updater-osx-x64.zip osx-x64/*

rm -Rf win-x64
rm -Rf linux-x64
rm -Rf osx-x64


