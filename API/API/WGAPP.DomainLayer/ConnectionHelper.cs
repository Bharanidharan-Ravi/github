//using System;
//using System.Collections.Generic;
//using System.Threading;
//using Microsoft.Extensions.Configuration;
//using SAPbobsCOM;

//namespace WGAPP.DomainLayer.Connection
//{
//    public class ConnectionHelper
//    {
//        private readonly IConfiguration _configuration;
//        private static readonly Dictionary<string, Company> _companyConnections = new Dictionary<string, Company>();
//        private static readonly Dictionary<string, Timer> _disconnectTimers = new Dictionary<string, Timer>(); // Store disconnect timers for each userKey
//        private static readonly object _lock = new object(); // Lock for thread safety

//        private int _errorCode = 0;
//        private string _errorMessage = string.Empty;

//        private readonly string _userKey;
//        private const int DisconnectInterval = 30 * 60 * 1000; // 30 minutes in milliseconds

//        // Constructor accepting IConfiguration
//        public ConnectionHelper(IConfiguration configuration)
//        {
//            _configuration = configuration;

//            // Build userKey from configuration values (USERID and DatabaseName)
//            var userId = _configuration["UserDetail:USERID"];
//            var companyDb = _configuration["UserDetail:DatabaseName"];
//            _userKey = $"{userId}_{companyDb}";
//        }

//        // Establish a connection based on the userKey
//        public async Task<int> EstablishConnection()
//        {
//            lock (_lock)
//            {
//                // Check if the connection already exists and is connected
//                if (_companyConnections.ContainsKey(_userKey) && _companyConnections[_userKey].Connected)
//                {
//                    // Reset the disconnect timer when reusing the connection
//                    ResetDisconnectTimer();
//                    return 0;
//                }

//                // Initialize new Company connection
//                var newCompany = new Company
//                {
//                    CompanyDB = _configuration["UserDetail:DatabaseName"],
//                    SLDServer = _configuration["SAPConfiguration:SldServer"],
//                    LicenseServer = _configuration["SAPConfiguration:LicenseServer"],
//                    Server = _configuration["SAPConfiguration:Server"],
//                    DbUserName = _configuration["DBConfiguration:Username"],
//                    DbPassword = _configuration["DBConfiguration:Password"],
//                    UserName = _configuration["SAPConfiguration:User"],
//                    Password = _configuration["SAPConfiguration:Password"],
//                    DbServerType = BoDataServerTypes.dst_MSSQL2019
//                };

//                // Handle environment-specific settings (production or staging)
//                string env = _configuration["Environment"];
//                if (env?.ToLower() == "production")
//                {
//                    newCompany.UseTrusted = false;
//                }

//                // Try to connect to SAP
//                int result = newCompany.Connect();
//                if (result != 0)
//                {
//                    newCompany.GetLastError(out _errorCode, out _errorMessage);
//                    throw new ApplicationException($"SAP connection failed: {_errorMessage} (Code {_errorCode})");
//                }

//                // Store the connection in the dictionary based on userKey
//                _companyConnections[_userKey] = newCompany;

//                // Start the disconnect timer for this user
//                StartDisconnectTimer();

//                return result;
//            }
//        }

//        // Get the company object (SAP connection)
//        public Company GetCompany()
//        {
//            lock (_lock)
//            {
//                // Return the connected company
//                if (_companyConnections.ContainsKey(_userKey) && _companyConnections[_userKey].Connected)
//                {
//                    // Reset the disconnect timer when the connection is used
//                    ResetDisconnectTimer();
//                    return _companyConnections[_userKey];
//                }

//                throw new InvalidOperationException("SAP connection not established.");
//            }
//        }

//        // Disconnect the connection (manual disconnect)
//        public void Disconnect()
//        {
//            lock (_lock)
//            {
//                if (_companyConnections.ContainsKey(_userKey) && _companyConnections[_userKey].Connected)
//                {
//                    _companyConnections[_userKey].Disconnect();
//                    _companyConnections.Remove(_userKey);

//                    // Stop and dispose of the disconnect timer
//                    StopDisconnectTimer();

//                    // Clean up the dictionary for the userKey
//                    if (_disconnectTimers.ContainsKey(_userKey))
//                    {
//                        _disconnectTimers[_userKey]?.Dispose();
//                        _disconnectTimers.Remove(_userKey);
//                    }
//                }
//            }
//        }

//        // Start the timer to disconnect after 30 minutes of inactivity
//        private void StartDisconnectTimer()
//        {
//            // Dispose of the previous timer (if exists)
//            if (_disconnectTimers.ContainsKey(_userKey))
//            {
//                _disconnectTimers[_userKey]?.Dispose();
//                _disconnectTimers.Remove(_userKey);
//            }

//            // Create a new timer that will trigger the disconnect after 30 minutes of inactivity
//            var timer = new Timer(DisconnectCallback, null, DisconnectInterval, Timeout.Infinite);
//            _disconnectTimers[_userKey] = timer;
//        }

//        // Stop the timer
//        private void StopDisconnectTimer()
//        {
//            if (_disconnectTimers.ContainsKey(_userKey))
//            {
//                _disconnectTimers[_userKey]?.Change(Timeout.Infinite, Timeout.Infinite); // Stop the timer
//            }
//        }

//        // Reset the disconnect timer when the connection is reused
//        private void ResetDisconnectTimer()
//        {
//            if (_disconnectTimers.ContainsKey(_userKey))
//            {
//                _disconnectTimers[_userKey]?.Change(DisconnectInterval, Timeout.Infinite); // Reset timer
//            }
//        }

//        // Callback to handle disconnection after the inactivity period
//        private void DisconnectCallback(object state)
//        {
//            Disconnect();
//        }

//        // Static method to get the instance of ConnectionHelper and establish the connection
//        public static ConnectionHelper GetInstance(IConfiguration configuration)
//        {
//            var helper = new ConnectionHelper(configuration);
//            helper.EstablishConnection().GetAwaiter().GetResult();
//            return helper;
//        }
//    }
//}





////public class ConnectionHelper
////{
////    private readonly IConfiguration _configuration;
////    private static Company _company = null; // Static to reuse connection
////    private static Timer _disconnectTimer; // Timer for automatic disconnection
////    private static readonly object _lock = new object(); // Lock for thread safety
////    private int _connectionResult;
////    private readonly string _companyName;
////    private int _errorCode = 0;
////    private const int connectionTimeout = 30000;
////    private string _errorMessage = string.Empty;
////    private const int DisconnectInterval = 30 * 60 * 1000; // 30 minutes in milliseconds

////    // Constructor accepting IConfiguration
////    public ConnectionHelper(IConfiguration configuration)
////    {
////        _configuration = configuration;
////        _companyName = _configuration["UserDetail:DatabaseName"];
////        //_company = new SAPbobsCOM.Company();
////        //_company.CompanyDB = _companyName;
////        //EstablishConnection(_configuration["UserDetail:DatabaseName"]);
////    }

////    // Static method to get the instance of ConnectionHelper and establish the connection
////    public static ConnectionHelper GetInstance(IConfiguration configuration, string companyName)
////    {
////        var helper = new ConnectionHelper(configuration);
////        helper.EstablishConnection(companyName).GetAwaiter().GetResult();
////        return helper;
////    }

////    // Method to establish connection
////    public async Task<int> EstablishConnection(string companyName)
////    {
////        if (_company != null && _company.Connected)
////        {
////            // Reset the disconnect timer when reusing the connection
////            ResetDisconnectTimer();
////            return 0;
////        }

////        lock (_lock)
////        {
////            if (_company == null || !_company.Connected)
////            {
////                _company = new Company
////                {
////                    CompanyDB = _companyName,
////                    SLDServer = Convert.ToString(_configuration["SAPConfiguration:SldServer"]),
////                    LicenseServer = Convert.ToString(_configuration["SAPConfiguration:LicenseServer"]),
////                    Server = Convert.ToString(_configuration["SAPConfiguration:Server"]),
////                    DbUserName = Convert.ToString(_configuration["DBConfiguration:Username"]),
////                    DbPassword = Convert.ToString(_configuration["DBConfiguration:Password"]),
////                    UserName = Convert.ToString(_configuration["SAPConfiguration:User"]),
////                    Password = Convert.ToString(_configuration["SAPConfiguration:Password"]),
////                    DbServerType = BoDataServerTypes.dst_MSSQL2019
////                };

////                string env = Convert.ToString(_configuration["Environment"]);
////                if (env.ToLower() == "production")
////                {
////                    _company.DbServerType = BoDataServerTypes.dst_MSSQL2019;
////                    _company.UseTrusted = false;
////                }
////                else if (env.ToLower() == "staging")
////                {
////                    _company.DbServerType = BoDataServerTypes.dst_MSSQL2019;
////                }

////                try
////                {
////                    //using (var cts = new CancellationTokenSource(connectionTimeout))
////                    //{
////                    //    _connectionResult = await Task.Run(() =>
////                    //    {
////                    //        _company.Connect();
////                    //    }, cts.Token);


////                    _connectionResult = _company.Connect();

////                    if (_connectionResult != 0)
////                        {
////                            _company.GetLastError(out _errorCode, out _errorMessage);
////                            throw new ApplicationException($"Could not connect to SAP. Error code - {_errorCode}. Error message - {_errorMessage}");
////                        }
////                        //    _company.GetLastError(out _errorCode, out _errorMessage);
////                        //    throw new ApplicationException($"Could not connect to SAP. Error code - {_errorCode}. Error message - {_errorMessage}");
////                        //}

////                        // Start the disconnect timer
////                        StartDisconnectTimer();

////                }
////                catch (Exception ex)
////                {
////                    throw new ApplicationException(ex.Message);
////                    //throw new ApplicationException($"Could not connect to SAP. Error code - {_errorCode}. Error message - {_errorMessage}");
////                }
////            }
////        }

////        return _connectionResult;
////    }

////    // Method to get the established company connection
////    public Company GetCompany()
////    {
////        if (_company == null || !_company.Connected)
////        {
////            throw new InvalidOperationException("SAP connection is not established.");
////        }

////        // Reset the disconnect timer each time the connection is used
////        ResetDisconnectTimer();
////        return _company;
////    }

////    // Method to disconnect the connection manually, if needed
////    public void Disconnect()
////    {
////        lock (_lock)
////        {
////            if (_company != null && _company.Connected)
////            {
////                _company.Disconnect();
////                _company = null;
////                StopDisconnectTimer();
////            }
////        }
////    }

////    // Start the timer to disconnect after 30 minutes of inactivity
////    private void StartDisconnectTimer()
////    {
////        _disconnectTimer = new Timer(DisconnectCallback, null, DisconnectInterval, Timeout.Infinite);
////    }

////    // Stop the timer
////    private void StopDisconnectTimer()
////    {
////        _disconnectTimer?.Change(Timeout.Infinite, Timeout.Infinite);
////        _disconnectTimer?.Dispose();
////        _disconnectTimer = null;
////    }

////    // Reset the timer when the connection is reused
////    private void ResetDisconnectTimer()
////    {
////        if (_disconnectTimer != null)
////        {
////            _disconnectTimer.Change(DisconnectInterval, Timeout.Infinite);
////        }
////    }

////    // Callback method to handle disconnection when the timer elapses
////    private void DisconnectCallback(object state)
////    {
////        Disconnect();
////    }
////}