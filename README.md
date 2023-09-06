# betacrew-aditya

Instructions for Running the Client Application:

1. **Start the BetaCrew Exchange Server:**

   - Download the [betacrew_exchange_server.zip](https://github.com/aditya-bsingh5/betacrew-aditya/files/12538214/betacrew_exchange_server.zip) file.
   - Extract the contents of the ZIP file.
   - Open your local machine terminal.
   - Navigate to the extracted folder.
   - Run the BetaCrew exchange server with the following command:
     ```
     node main.js
     ```
   - Ensure that you have Node.js version 16.17.0 or higher installed on your system.

2. **Run the Client Application:**

   - Ensure you have .NET 7.0 or a higher version installed on your system.
   - Open your terminal.
   - Navigate to the directory containing the Client Application.
   - Perform the following steps to clean, build, and run the application:
     - Clean the project: `dotnet clean`
     - Restore project dependencies: `dotnet restore`
     - Build the project: `dotnet build`
     - Run the application: `dotnet run`

3. **Configure TCP Connection:**

   - The application uses the localhost IP address for TCP connection.
   - You can configure a different IP address from the App.config file if needed.

4. **Logging and Event Handling:**

   - The application logs events on the console and in a log file.
   - Log file path: "EVENT/EVENTLOG.txt" in the repository.
   - You can configure the log file path from the App.config file.

5. **Output Storage:**

   - The output of the application is stored in a JSON file.
   - Output file path: "OUTPUT/OUTPUT.json" in the repository.
   - You can configure the output file path from the App.config file.

   Note: The number of packets may vary between 13-14 due to the logic for missing sequences. It is assumed that the last packet is never missed, which can lead to different packet counts in various runs.
