# WhisperTranslator

Whisper Translator has been designed to allow users of the NHS.
This Open Source Solution, uses Azure OPen AI Services and Azure SQL DB to create a Web App which runs in dot net 9.0 on mobile devices / Tablet and Desktop Devices.
This solution allows a user once logged in to create a Trnaslation and auto listen and transcribe from any language into english, it then will allow the user to reply back to and it play and show the infomration in the same language detected.
It stores all this data in an Azure SQL DB which you can the re-open the transcriptions if needed for a later cause.

To get started with this project just install Visual Studio and download the Files,

goto open project and then select the WhisperTranslator.sln

Update the appsettings.json file with details from your azure portal for Azure SQL DB and your Wisper Open AI screen

Restore the database file and publish to an Azure Web App 

Done
