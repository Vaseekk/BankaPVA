using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BankaPVA
{
    // Login for admin is admin:admin
    // Base abstract Account class
    public abstract class Account
    {
        public int AccountId { get; protected set; }
        public int OwnerId { get; protected set; }
        public double Balance { get; protected set; }
        public DateTime CreationDate { get; protected set; }

        public Account(int accountId, int ownerId, double balance, DateTime? creationDate = null)
        {
            AccountId = accountId;
            OwnerId = ownerId;
            Balance = balance;
            CreationDate = creationDate ?? DateTime.Now;
        }

        public virtual double Deposit(double amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Invalid amount. Please enter a positive number.");

            Balance += amount;
            Logger.Info($"Deposit, Amount: {amount:C}, New Balance: {Balance:C}");
            return Balance;
        }

        public virtual double Withdraw(double amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Invalid amount. Please enter a positive number.");

            if (amount > Balance)
                throw new InvalidOperationException($"Insufficient funds. Current balance: {Balance:C}");

            Balance -= amount;
            Logger.Info($"Withdraw, Amount: {amount:C}, New Balance: {Balance:C}");
            return Balance;
        }

        public double GetBalance()
        {
            return Balance;
        }

        protected void LogTransaction(string type, double amount)
        {
            Logger.Info($"Transaction, Account: {AccountId}, Type: {type}, Amount: {amount:C}, New Balance: {Balance:C}");
        }

        public abstract string GetAccountType();
        public abstract double CalculateMonthlyInterest(int days = 30);
    }

    // CheckingAccount implementation
    public class CheckingAccount : Account
    {
        public int? LinkedSavingsAccountId { get; set; }

        public CheckingAccount(int accountId, int ownerId, double balance, int? linkedSavingsAccountId = null, DateTime? creationDate = null)
            : base(accountId, ownerId, balance, creationDate)
        {
            LinkedSavingsAccountId = linkedSavingsAccountId;
        }

        public double TransferToSavings(double amount, SavingsAccount savingsAccount)
        {
            if (amount <= 0)
                throw new ArgumentException("Invalid amount. Please enter a positive number.");

            if (amount > Balance)
                throw new InvalidOperationException($"Insufficient funds. Current balance: {Balance:C}");

            Balance -= amount;
            savingsAccount.Deposit(amount);
            LogTransaction("Transfer to Savings", -amount);
            return Balance;
        }

        public override string GetAccountType()
        {
            return "Checking Account";
        }

        public override double CalculateMonthlyInterest(int days = 30)
        {
            // Checking account doesn't earn interest
            return 0;
        }
    }

    // SavingsAccount implementation
    public class SavingsAccount : Account
    {
        public double InterestRate { get; set; }
        public double DailyWithdrawalLimit { get; set; }
        private List<Tuple<DateTime, double>> balanceHistory = new List<Tuple<DateTime, double>>();

        public SavingsAccount(int accountId, int ownerId, double balance, double interestRate, double dailyWithdrawalLimit, DateTime? creationDate = null)
            : base(accountId, ownerId, balance, creationDate)
        {
            InterestRate = interestRate;
            DailyWithdrawalLimit = dailyWithdrawalLimit;
            balanceHistory.Add(new Tuple<DateTime, double>(TimeSimulator.CurrentTime, balance));
        }

        public override double Deposit(double amount)
        {
            double newBalance = base.Deposit(amount);
            balanceHistory.Add(new Tuple<DateTime, double>(TimeSimulator.CurrentTime, newBalance));
            return newBalance;
        }

        public override double Withdraw(double amount)
        {
            if (amount > DailyWithdrawalLimit)
                throw new InvalidOperationException($"Daily withdrawal limit exceeded. Maximum: {DailyWithdrawalLimit:C}");

            double newBalance = base.Withdraw(amount);
            balanceHistory.Add(new Tuple<DateTime, double>(TimeSimulator.CurrentTime, newBalance));
            return newBalance;
        }

        public override string GetAccountType()
        {
            return "Savings Account";
        }

        public override double CalculateMonthlyInterest(int days = 30)
        {
            double weightedBalance = CalculateWeightedAverageBalance(days);
            double monthlyInterest = (weightedBalance * InterestRate) / 12;

            // Round to 2 decimal places
            monthlyInterest = Math.Round(monthlyInterest, 2);

            if (monthlyInterest > 0)
            {
                Balance += monthlyInterest;
                balanceHistory.Add(new Tuple<DateTime, double>(TimeSimulator.CurrentTime, Balance));
                LogTransaction("Interest", monthlyInterest);
            }

            return monthlyInterest;
        }

        protected double CalculateWeightedAverageBalance(int days)
        {
            if (balanceHistory.Count <= 1)
                return Balance;

            DateTime endDate = TimeSimulator.CurrentTime;
            DateTime startDate = endDate.AddDays(-days);

            List<Tuple<DateTime, double>> relevantHistory = balanceHistory
                .FindAll(h => h.Item1 >= startDate && h.Item1 <= endDate)
                .OrderBy(h => h.Item1)
                .ToList();

            if (relevantHistory.Count == 0)
                return Balance;

            double weightedSum = 0;
            double totalDays = 0;

            for (int i = 0; i < relevantHistory.Count - 1; i++)
            {
                var current = relevantHistory[i];
                var next = relevantHistory[i + 1];

                double daysBetween = (next.Item1 - current.Item1).TotalDays;
                weightedSum += current.Item2 * daysBetween;
                totalDays += daysBetween;
            }

            // Add the last period to the current date
            var last = relevantHistory.Last();
            double lastDays = (endDate - last.Item1).TotalDays;
            weightedSum += last.Item2 * lastDays;
            totalDays += lastDays;

            if (totalDays > 0)
                return weightedSum / totalDays;
            else
                return Balance;
        }
    }

    // StudentSavingsAccount implementation
    public class StudentSavingsAccount : SavingsAccount
    {
        public double SingleWithdrawalLimit { get; set; }

        public StudentSavingsAccount(int accountId, int ownerId, double balance, double interestRate,
                                    double dailyWithdrawalLimit, double singleWithdrawalLimit,
                                    DateTime? creationDate = null)
            : base(accountId, ownerId, balance, interestRate, dailyWithdrawalLimit, creationDate)
        {
            SingleWithdrawalLimit = singleWithdrawalLimit;
        }

        public override double Withdraw(double amount)
        {
            if (amount > SingleWithdrawalLimit)
                throw new InvalidOperationException($"Single withdrawal limit exceeded. Maximum: {SingleWithdrawalLimit:C}");

            return base.Withdraw(amount);
        }

        public override string GetAccountType()
        {
            return "Student Savings Account";
        }
    }

    // CreditAccount implementation
    public class CreditAccount : Account
    {
        public double CreditLimit { get; set; }
        public double InterestRate { get; set; }
        public DateTime GracePeriodEnd { get; set; }
        private List<Tuple<DateTime, double>> balanceHistory = new List<Tuple<DateTime, double>>();

        public CreditAccount(int accountId, int ownerId, double balance, double creditLimit,
                            double interestRate, DateTime gracePeriodEnd, DateTime? creationDate = null)
            : base(accountId, ownerId, balance, creationDate)
        {
            CreditLimit = creditLimit;
            InterestRate = interestRate;
            GracePeriodEnd = gracePeriodEnd;
            balanceHistory.Add(new Tuple<DateTime, double>(TimeSimulator.CurrentTime, balance));
        }

        public double Borrow(double amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Invalid amount. Please enter a positive number.");

            if (Math.Abs(Balance) + amount > CreditLimit)
                throw new InvalidOperationException($"Credit limit exceeded. Available credit: {CreditLimit - Math.Abs(Balance):C}");

            Balance -= amount;
            balanceHistory.Add(new Tuple<DateTime, double>(TimeSimulator.CurrentTime, Balance));
            LogTransaction("Borrow", -amount);
            return Balance;
        }

        public double Repay(double amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Invalid amount. Please enter a positive number.");

            Balance += amount;
            balanceHistory.Add(new Tuple<DateTime, double>(TimeSimulator.CurrentTime, Balance));
            LogTransaction("Repay", amount);
            return Balance;
        }

        public override double Deposit(double amount)
        {
            return Repay(amount);
        }

        public override double Withdraw(double amount)
        {
            return Borrow(amount);
        }

        public override string GetAccountType()
        {
            return "Credit Account";
        }

        public double GetDebt()
        {
            return Balance < 0 ? Math.Abs(Balance) : 0;
        }

        public override double CalculateMonthlyInterest(int days = 30)
        {
            // Skip interest calculation during grace period
            if (TimeSimulator.CurrentTime <= GracePeriodEnd)
            {
                Console.WriteLine($"Account is in grace period until {GracePeriodEnd:yyyy-MM-dd}");
                return 0;
            }

            if (Balance >= 0)
                return 0;  // No interest if no debt

            double weightedBalance = CalculateWeightedAverageBalance(days);
            double monthlyInterest = (Math.Abs(weightedBalance) * InterestRate) / 12;

            // Interest is negative for credit accounts (cost to customer)
            monthlyInterest = -Math.Round(monthlyInterest, 2);

            Balance += monthlyInterest;  // Decrease balance by interest amount
            balanceHistory.Add(new Tuple<DateTime, double>(TimeSimulator.CurrentTime, Balance));
            LogTransaction("Interest", monthlyInterest);

            return monthlyInterest;
        }

        private double CalculateWeightedAverageBalance(int days)
        {
            if (balanceHistory.Count <= 1)
                return Balance;

            DateTime endDate = TimeSimulator.CurrentTime;
            DateTime startDate = endDate.AddDays(-days);

            List<Tuple<DateTime, double>> relevantHistory = balanceHistory
                .FindAll(h => h.Item1 >= startDate && h.Item1 <= endDate)
                .OrderBy(h => h.Item1)
                .ToList();

            if (relevantHistory.Count == 0)
                return Balance;

            double weightedSum = 0;
            double totalDays = 0;

            for (int i = 0; i < relevantHistory.Count - 1; i++)
            {
                var current = relevantHistory[i];
                var next = relevantHistory[i + 1];

                double daysBetween = (next.Item1 - current.Item1).TotalDays;
                weightedSum += current.Item2 * daysBetween;
                totalDays += daysBetween;
            }

            // Add the last period to the current date
            var last = relevantHistory.Last();
            double lastDays = (endDate - last.Item1).TotalDays;
            weightedSum += last.Item2 * lastDays;
            totalDays += lastDays;

            if (totalDays > 0)
                return weightedSum / totalDays;
            else
                return Balance;
        }
    }

    // User class for authentication and role management
    public class User
    {
        public int UserId { get; private set; }
        public string Username { get; private set; }
        public string PasswordHash { get; private set; }
        public string Salt { get; private set; }
        public UserRole Role { get; set; }
        public DateTime CreatedAt { get; private set; }

        public enum UserRole
        {
            Client,
            Banker,
            Admin
        }

        public User(int userId, string username, string passwordHash, string salt, UserRole role, DateTime? createdAt = null)
        {
            UserId = userId;
            Username = username;
            PasswordHash = passwordHash;
            Salt = salt;
            Role = role;
            CreatedAt = createdAt ?? DateTime.Now;
        }

        public static string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        public static string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                string saltedPassword = password + salt;
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public static bool VerifyPassword(string inputPassword, string storedHash, string salt)
        {
            string hashedInput = HashPassword(inputPassword, salt);
            return hashedInput == storedHash;
        }
    }

    // Database manager class
    public class DatabaseManager
    {
        private string connectionString;

        public DatabaseManager(string dbPath = "bank.db", bool resetDatabase = false)
        {
            try
            {
                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Set connection string
                connectionString = $"Data Source={dbPath};Version=3;";

                // Only initialize database if it's a new database or reset was requested
                if (!File.Exists(dbPath) || resetDatabase)
                {
                    if (resetDatabase && File.Exists(dbPath))
                    {
                        File.Delete(dbPath);
                        Console.WriteLine("Database reset requested - creating new database.");
                        Console.WriteLine("Press anything to continue...");
                        Console.ReadLine();
                    }
                    InitializeDatabase();
                }
                else
                {
                    Console.WriteLine("Using existing database.");
                    Console.WriteLine("Press anything to continue...");
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing database: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void InitializeDatabase()
        {
            try
            {
                Logger.Info("Starting database initialization...");
                using (var conn = new SQLiteConnection(connectionString))
                {
                    Logger.Info("Opening database connection...");
                    conn.Open();
                    Logger.Info("Database connection opened successfully.");

                    // Create Users table
                    Logger.Info("Creating users table...");
                    string createUsersTable = @"
                        CREATE TABLE IF NOT EXISTS users (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            username TEXT NOT NULL UNIQUE,
                            passwordHash TEXT NOT NULL,
                            salt TEXT NOT NULL,
                            role TEXT NOT NULL CHECK(role IN ('Client', 'Banker', 'Admin')),
                            createdAt DATETIME DEFAULT CURRENT_TIMESTAMP
                        );";

                    using (var cmd = new SQLiteCommand(createUsersTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                        Logger.Info("Users table created successfully.");
                    }

                    // Create Accounts table
                    Logger.Info("Creating accounts table...");
                    string createAccountsTable = @"
                        CREATE TABLE IF NOT EXISTS accounts (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ownerId INTEGER NOT NULL,
                            type TEXT NOT NULL,
                            balance REAL NOT NULL DEFAULT 0.0,
                            creationDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                            interestRate REAL,
                            dailyWithdrawalLimit REAL,
                            singleWithdrawalLimit REAL,
                            creditLimit REAL,
                            gracePeriodEnd DATETIME,
                            linkedSavingsAccountId INTEGER,
                            FOREIGN KEY (ownerId) REFERENCES users(id)
                        );";

                    using (var cmd = new SQLiteCommand(createAccountsTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                        Logger.Info("Accounts table created successfully.");
                    }

                    // Create Transactions table
                    Logger.Info("Creating transactions table...");
                    string createTransactionsTable = @"
                        CREATE TABLE IF NOT EXISTS transactions (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            accountId INTEGER NOT NULL,
                            type TEXT NOT NULL,
                            amount REAL NOT NULL,
                            newBalance REAL NOT NULL,
                            datetime DATETIME DEFAULT CURRENT_TIMESTAMP,
                            FOREIGN KEY (accountId) REFERENCES accounts(id)
                        );";

                    using (var cmd = new SQLiteCommand(createTransactionsTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                        Logger.Info("Transactions table created successfully.");
                    }

                    // Create admin user if none exists
                    Logger.Info("Checking for admin user...");
                    string checkAdmin = "SELECT COUNT(*) FROM users WHERE role = 'Admin';";
                    using (var cmd = new SQLiteCommand(checkAdmin, conn))
                    {
                        int adminCount = Convert.ToInt32(cmd.ExecuteScalar());
                        if (adminCount == 0)
                        {
                            Logger.Info("Creating default admin user...");
                            // Create default admin user
                            string salt = User.GenerateSalt();
                            string passwordHash = User.HashPassword("admin", salt);

                            string insertAdmin = "INSERT INTO users (username, passwordHash, salt, role) VALUES ('admin', @passwordHash, @salt, 'Admin');";
                            using (var insertCmd = new SQLiteCommand(insertAdmin, conn))
                            {
                                insertCmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                                insertCmd.Parameters.AddWithValue("@salt", salt);
                                insertCmd.ExecuteNonQuery();
                                Logger.Info("Default admin user created successfully.");
                            }
                        }
                        else
                        {
                            Logger.Info("Admin user already exists.");
                        }
                    }
                }

                // Verify the database file was created
                string dbPath = connectionString.Split('=')[1].Split(';')[0].Trim('"');
                if (!File.Exists(dbPath))
                {
                    var ex = new Exception($"Database file was not created at: {dbPath}");
                    Logger.Error("Database initialization failed", ex);
                    throw ex;
                }
                Logger.Info($"Database initialized successfully at: {dbPath}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during database initialization", ex);
                throw;
            }
        }

        // User management methods
        public User GetUserByUsername(string username)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT id, username, passwordHash, salt, role, createdAt FROM users WHERE username = @username;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int userId = Convert.ToInt32(reader["id"]);
                            string passwordHash = reader["passwordHash"].ToString();
                            string salt = reader["salt"].ToString();
                            User.UserRole role = (User.UserRole)Enum.Parse(typeof(User.UserRole), reader["role"].ToString());
                            DateTime createdAt = Convert.ToDateTime(reader["createdAt"]);

                            return new User(userId, username, passwordHash, salt, role, createdAt);
                        }
                    }
                }
            }

            return null;
        }

        public User CreateUser(string username, string password, User.UserRole role)
        {
            string salt = User.GenerateSalt();
            string passwordHash = User.HashPassword(password, salt);

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "INSERT INTO users (username, passwordHash, salt, role) VALUES (@username, @passwordHash, @salt, @role); SELECT last_insert_rowid();";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                    cmd.Parameters.AddWithValue("@salt", salt);
                    cmd.Parameters.AddWithValue("@role", role.ToString());

                    int userId = Convert.ToInt32(cmd.ExecuteScalar());
                    return new User(userId, username, passwordHash, salt, role);
                }
            }
        }

        public bool DeleteUser(string username)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "DELETE FROM users WHERE username = @username;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        public List<User> GetAllUsers()
        {
            List<User> users = new List<User>();

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT id, username, passwordHash, salt, role, createdAt FROM users;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int userId = Convert.ToInt32(reader["id"]);
                            string username = reader["username"].ToString();
                            string passwordHash = reader["passwordHash"].ToString();
                            string salt = reader["salt"].ToString();
                            User.UserRole role = (User.UserRole)Enum.Parse(typeof(User.UserRole), reader["role"].ToString());
                            DateTime createdAt = Convert.ToDateTime(reader["createdAt"]);

                            users.Add(new User(userId, username, passwordHash, salt, role, createdAt));
                        }
                    }
                }
            }

            return users;
        }

        public bool UpdateUserRole(string username, User.UserRole newRole)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "UPDATE users SET role = @role WHERE username = @username;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@role", newRole.ToString());
                    cmd.Parameters.AddWithValue("@username", username);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        // Account management methods
        public int CreateCheckingAccount(int ownerId, double initialBalance = 0, int? linkedSavingsAccountId = null)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = @"
                    INSERT INTO accounts (ownerId, type, balance, linkedSavingsAccountId) 
                    VALUES (@ownerId, 'Checking', @balance, @linkedSavingsAccountId);
                    SELECT last_insert_rowid();";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ownerId", ownerId);
                    cmd.Parameters.AddWithValue("@balance", initialBalance);
                    cmd.Parameters.AddWithValue("@linkedSavingsAccountId", linkedSavingsAccountId as object ?? DBNull.Value);

                    int accountId = Convert.ToInt32(cmd.ExecuteScalar());

                    // Log initial deposit if balance > 0
                    if (initialBalance > 0)
                    {
                        LogTransactionToDB(accountId, "Initial Deposit", initialBalance, initialBalance);
                    }

                    return accountId;
                }
            }
        }

        public int CreateSavingsAccount(int ownerId, double interestRate, double dailyWithdrawalLimit, double initialBalance = 0)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = @"
                    INSERT INTO accounts (ownerId, type, balance, interestRate, dailyWithdrawalLimit) 
                    VALUES (@ownerId, 'Savings', @balance, @interestRate, @dailyWithdrawalLimit);
                    SELECT last_insert_rowid();";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ownerId", ownerId);
                    cmd.Parameters.AddWithValue("@balance", initialBalance);
                    cmd.Parameters.AddWithValue("@interestRate", interestRate);
                    cmd.Parameters.AddWithValue("@dailyWithdrawalLimit", dailyWithdrawalLimit);

                    int accountId = Convert.ToInt32(cmd.ExecuteScalar());

                    if (initialBalance > 0)
                    {
                        LogTransactionToDB(accountId, "Initial Deposit", initialBalance, initialBalance);
                    }

                    return accountId;
                }
            }
        }

        public int CreateStudentSavingsAccount(int ownerId, double interestRate, double dailyWithdrawalLimit,
                                             double singleWithdrawalLimit, double initialBalance = 0)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = @"
                    INSERT INTO accounts (ownerId, type, balance, interestRate, dailyWithdrawalLimit, singleWithdrawalLimit) 
                    VALUES (@ownerId, 'StudentSavings', @balance, @interestRate, @dailyWithdrawalLimit, @singleWithdrawalLimit);
                    SELECT last_insert_rowid();";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ownerId", ownerId);
                    cmd.Parameters.AddWithValue("@balance", initialBalance);
                    cmd.Parameters.AddWithValue("@interestRate", interestRate);
                    cmd.Parameters.AddWithValue("@dailyWithdrawalLimit", dailyWithdrawalLimit);
                    cmd.Parameters.AddWithValue("@singleWithdrawalLimit", singleWithdrawalLimit);

                    int accountId = Convert.ToInt32(cmd.ExecuteScalar());

                    if (initialBalance > 0)
                    {
                        LogTransactionToDB(accountId, "Initial Deposit", initialBalance, initialBalance);
                    }

                    return accountId;
                }
            }
        }

        public int CreateCreditAccount(int ownerId, double creditLimit, double interestRate, int gracePeriodDays = 30)
        {
            DateTime gracePeriodEnd = DateTime.Now.AddDays(gracePeriodDays);

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = @"
                    INSERT INTO accounts (ownerId, type, balance, creditLimit, interestRate, gracePeriodEnd) 
                    VALUES (@ownerId, 'Credit', 0, @creditLimit, @interestRate, @gracePeriodEnd);
                    SELECT last_insert_rowid();";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ownerId", ownerId);
                    cmd.Parameters.AddWithValue("@creditLimit", creditLimit);
                    cmd.Parameters.AddWithValue("@interestRate", interestRate);
                    cmd.Parameters.AddWithValue("@gracePeriodEnd", gracePeriodEnd);

                    int accountId = Convert.ToInt32(cmd.ExecuteScalar());
                    return accountId;
                }
            }
        }

        public List<Account> GetUserAccounts(int userId)
        {
            List<Account> accounts = new List<Account>();

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT * FROM accounts WHERE ownerId = @userId;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int accountId = Convert.ToInt32(reader["id"]);
                            string type = reader["type"].ToString();
                            double balance = Convert.ToDouble(reader["balance"]);
                            DateTime creationDate = Convert.ToDateTime(reader["creationDate"]);

                            Account account = null;

                            switch (type)
                            {
                                case "Checking":
                                    int? linkedSavingsId = reader["linkedSavingsAccountId"] != DBNull.Value ?
                                                           (int?)Convert.ToInt32(reader["linkedSavingsAccountId"]) : null;
                                    account = new CheckingAccount(accountId, userId, balance, linkedSavingsId, creationDate);
                                    break;

                                case "Savings":
                                    double interestRate = Convert.ToDouble(reader["interestRate"]);
                                    double dailyWithdrawalLimit = Convert.ToDouble(reader["dailyWithdrawalLimit"]);
                                    account = new SavingsAccount(accountId, userId, balance, interestRate, dailyWithdrawalLimit, creationDate);
                                    break;

                                case "StudentSavings":
                                    double studInterestRate = Convert.ToDouble(reader["interestRate"]);
                                    double studDailyLimit = Convert.ToDouble(reader["dailyWithdrawalLimit"]);
                                    double singleWithdrawalLimit = Convert.ToDouble(reader["singleWithdrawalLimit"]);
                                    account = new StudentSavingsAccount(accountId, userId, balance, studInterestRate,
                                                                      studDailyLimit, singleWithdrawalLimit, creationDate);
                                    break;

                                case "Credit":
                                    double creditLimit = Convert.ToDouble(reader["creditLimit"]);
                                    double creditInterestRate = Convert.ToDouble(reader["interestRate"]);
                                    DateTime gracePeriodEnd = Convert.ToDateTime(reader["gracePeriodEnd"]);
                                    account = new CreditAccount(accountId, userId, balance, creditLimit,
                                                              creditInterestRate, gracePeriodEnd, creationDate);
                                    break;
                            }

                            if (account != null)
                            {
                                accounts.Add(account);
                            }
                        }
                    }
                }
            }

            return accounts;
        }

        public List<Account> GetAllAccounts()
        {
            List<Account> accounts = new List<Account>();

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT * FROM accounts;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int accountId = Convert.ToInt32(reader["id"]);
                            int ownerId = Convert.ToInt32(reader["ownerId"]);
                            string type = reader["type"].ToString();
                            double balance = Convert.ToDouble(reader["balance"]);
                            DateTime creationDate = Convert.ToDateTime(reader["creationDate"]);

                            Account account = null;

                            switch (type)
                            {
                                case "Checking":
                                    int? linkedSavingsId = reader["linkedSavingsAccountId"] != DBNull.Value ?
                                                           (int?)Convert.ToInt32(reader["linkedSavingsAccountId"]) : null;
                                    account = new CheckingAccount(accountId, ownerId, balance, linkedSavingsId, creationDate);
                                    break;

                                case "Savings":
                                    double interestRate = Convert.ToDouble(reader["interestRate"]);
                                    double dailyWithdrawalLimit = Convert.ToDouble(reader["dailyWithdrawalLimit"]);
                                    account = new SavingsAccount(accountId, ownerId, balance, interestRate, dailyWithdrawalLimit, creationDate);
                                    break;

                                case "StudentSavings":
                                    double studInterestRate = Convert.ToDouble(reader["interestRate"]);
                                    double studDailyLimit = Convert.ToDouble(reader["dailyWithdrawalLimit"]);
                                    double singleWithdrawalLimit = Convert.ToDouble(reader["singleWithdrawalLimit"]);
                                    account = new StudentSavingsAccount(accountId, ownerId, balance, studInterestRate,
                                                                      studDailyLimit, singleWithdrawalLimit, creationDate);
                                    break;


                                case "Credit":
                                    double creditLimit = Convert.ToDouble(reader["creditLimit"]);
                                    double creditInterestRate = Convert.ToDouble(reader["interestRate"]);
                                    DateTime gracePeriodEnd = Convert.ToDateTime(reader["gracePeriodEnd"]);
                                    account = new CreditAccount(accountId, ownerId, balance, creditLimit,
                                                              creditInterestRate, gracePeriodEnd, creationDate);
                                    break;
                            }

                            if (account != null)
                            {
                                accounts.Add(account);
                            }
                        }
                    }
                }
            }

            return accounts;
        }

        public Account GetAccountById(int accountId)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT * FROM accounts WHERE id = @accountId;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@accountId", accountId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int ownerId = Convert.ToInt32(reader["ownerId"]);
                            string type = reader["type"].ToString();
                            double balance = Convert.ToDouble(reader["balance"]);
                            DateTime creationDate = Convert.ToDateTime(reader["creationDate"]);

                            switch (type)
                            {
                                case "Checking":
                                    int? linkedSavingsId = reader["linkedSavingsAccountId"] != DBNull.Value ?
                                                           (int?)Convert.ToInt32(reader["linkedSavingsAccountId"]) : null;
                                    return new CheckingAccount(accountId, ownerId, balance, linkedSavingsId, creationDate);

                                case "Savings":
                                    double interestRate = Convert.ToDouble(reader["interestRate"]);
                                    double dailyWithdrawalLimit = Convert.ToDouble(reader["dailyWithdrawalLimit"]);
                                    return new SavingsAccount(accountId, ownerId, balance, interestRate, dailyWithdrawalLimit, creationDate);

                                case "StudentSavings":
                                    double studInterestRate = Convert.ToDouble(reader["interestRate"]);
                                    double studDailyLimit = Convert.ToDouble(reader["dailyWithdrawalLimit"]);
                                    double singleWithdrawalLimit = Convert.ToDouble(reader["singleWithdrawalLimit"]);
                                    return new StudentSavingsAccount(accountId, ownerId, balance, studInterestRate,
                                                                    studDailyLimit, singleWithdrawalLimit, creationDate);

                                case "Credit":
                                    double creditLimit = Convert.ToDouble(reader["creditLimit"]);
                                    double creditInterestRate = Convert.ToDouble(reader["interestRate"]);
                                    DateTime gracePeriodEnd = Convert.ToDateTime(reader["gracePeriodEnd"]);
                                    return new CreditAccount(accountId, ownerId, balance, creditLimit,
                                                            creditInterestRate, gracePeriodEnd, creationDate);
                            }
                        }
                    }
                }
            }

            return null;
        }

        public bool UpdateAccountBalance(int accountId, double newBalance)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "UPDATE accounts SET balance = @balance WHERE id = @accountId;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@balance", newBalance);
                    cmd.Parameters.AddWithValue("@accountId", accountId);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        public bool LinkSavingsAccount(int checkingAccountId, int savingsAccountId)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "UPDATE accounts SET linkedSavingsAccountId = @savingsId WHERE id = @checkingId AND type = 'Checking';";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@savingsId", savingsAccountId);
                    cmd.Parameters.AddWithValue("@checkingId", checkingAccountId);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        public bool CloseAccount(int accountId)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                // Check if the account has a zero balance
                string checkBalance = "SELECT balance FROM accounts WHERE id = @accountId;";
                using (var checkCmd = new SQLiteCommand(checkBalance, conn))
                {
                    checkCmd.Parameters.AddWithValue("@accountId", accountId);
                    object result = checkCmd.ExecuteScalar();
                    if (result != null)
                    {
                        double balance = Convert.ToDouble(result);
                        if (balance != 0)
                        {
                            return false; // Cannot close account with non-zero balance
                        }
                    }
                    else
                    {
                        return false; // Account doesn't exist
                    }
                }

                // Delete account if balance is zero
                string query = "DELETE FROM accounts WHERE id = @accountId;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@accountId", accountId);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        public List<Tuple<DateTime, string, double, double>> GetAccountTransactions(int accountId)
        {
            List<Tuple<DateTime, string, double, double>> transactions = new List<Tuple<DateTime, string, double, double>>();

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT datetime, type, amount, newBalance FROM transactions WHERE accountId = @accountId ORDER BY datetime DESC;";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@accountId", accountId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DateTime dateTime = Convert.ToDateTime(reader["datetime"]);
                            string type = reader["type"].ToString();
                            double amount = Convert.ToDouble(reader["amount"]);
                            double newBalance = Convert.ToDouble(reader["newBalance"]);

                            transactions.Add(new Tuple<DateTime, string, double, double>(dateTime, type, amount, newBalance));
                        }
                    }
                }
            }

            return transactions;
        }

        public void LogTransactionToDB(int accountId, string type, double amount, double newBalance)
        {
            try
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO transactions (accountId, type, amount, newBalance) VALUES (@accountId, @type, @amount, @newBalance);";
                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@accountId", accountId);
                        cmd.Parameters.AddWithValue("@type", type);
                        cmd.Parameters.AddWithValue("@amount", amount);
                        cmd.Parameters.AddWithValue("@newBalance", newBalance);
                        cmd.ExecuteNonQuery();
                    }
                }
                Logger.Info($"Transaction logged: Account {accountId}, Type {type}, Amount {amount:C}, New Balance {newBalance:C}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to log transaction", ex);
                throw;
            }
        }
    }

    // Time simulator class for testing time-dependent features
    public class TimeSimulator
    {
        private static DateTime simulatedTime;
        private static bool isSimulationEnabled = false;

        public static DateTime CurrentTime
        {
            get { return isSimulationEnabled ? simulatedTime : DateTime.Now; }
        }

        public static void EnableSimulation(DateTime startTime)
        {
            isSimulationEnabled = true;
            simulatedTime = startTime;
        }

        public static void DisableSimulation()
        {
            isSimulationEnabled = false;
        }

        public static void AdvanceTime(int days)
        {
            if (!isSimulationEnabled)
                throw new InvalidOperationException("Time simulation is not enabled");

            simulatedTime = simulatedTime.AddDays(days);
        }

        public static bool IsSimulationEnabled
        {
            get { return isSimulationEnabled; }
        }

        public static DateTime GetSimulatedTime
        {
            get { return simulatedTime; }
        }
    }

    // Bank system class that handles application logic
    public class BankSystem
    {
        private static DatabaseManager db;
        private static User currentUser;

        public BankSystem(string dbPath = "bank.db", bool resetDatabase = false)
        {
            db = new DatabaseManager(dbPath, resetDatabase);
        }

        // Authentication methods
        public User Login(string username, string password)
        {
            User user = db.GetUserByUsername(username);
            if (user != null && User.VerifyPassword(password, user.PasswordHash, user.Salt))
            {
                currentUser = user;
                Logger.Info($"User {username} logged in successfully");
                return user;
            }
            Logger.Warning($"Failed login attempt for username: {username}");
            return null;
        }

        public void Logout()
        {
            currentUser = null;
        }

        public User GetCurrentUser()
        {
            return currentUser;
        }

        // User management methods
        public User RegisterUser(string username, string password, User.UserRole role = User.UserRole.Client)
        {
            if (db.GetUserByUsername(username) != null)
            {
                throw new ArgumentException("Username already exists");
            }

            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                throw new ArgumentException("Username must be at least 3 characters");
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                throw new ArgumentException("Password must be at least 6 characters");
            }

            User newUser = db.CreateUser(username, password, role);
            return newUser;
        }

        public List<User> GetAllUsers()
        {
            if (currentUser == null || (currentUser.Role != User.UserRole.Admin && currentUser.Role != User.UserRole.Banker))
            {
                throw new UnauthorizedAccessException("Admin or Banker access required");
            }

            return db.GetAllUsers();
        }

        public bool ChangeUserRole(string username, User.UserRole newRole)
        {
            if (currentUser == null || currentUser.Role != User.UserRole.Admin)
            {
                throw new UnauthorizedAccessException("Admin access required");
            }

            return db.UpdateUserRole(username, newRole);
        }

        public bool DeleteUserAccount(string username)
        {
            if (currentUser == null || currentUser.Role != User.UserRole.Admin)
            {
                throw new UnauthorizedAccessException("Admin access required");
            }

            // Don't allow deleting your own account
            if (username == currentUser.Username)
            {
                throw new InvalidOperationException("Cannot delete your own account");
            }

            return db.DeleteUser(username);
        }

        // Account management methods
        public int CreateCheckingAccount(int ownerId, double initialDeposit = 0)
        {
            ValidateUserAccess(ownerId);
            return db.CreateCheckingAccount(ownerId, initialDeposit);
        }

        public int CreateSavingsAccount(int ownerId, double initialDeposit = 0, double interestRate = 0.03, double dailyWithdrawalLimit = 1000)
        {
            ValidateUserAccess(ownerId);
            return db.CreateSavingsAccount(ownerId, interestRate, dailyWithdrawalLimit, initialDeposit);
        }

        public int CreateStudentSavingsAccount(int ownerId, double initialDeposit = 0, double interestRate = 0.05,
                                             double dailyWithdrawalLimit = 500, double singleWithdrawalLimit = 200)
        {
            ValidateUserAccess(ownerId);
            return db.CreateStudentSavingsAccount(ownerId, interestRate, dailyWithdrawalLimit, singleWithdrawalLimit, initialDeposit);
        }

        public int CreateCreditAccount(int ownerId, double creditLimit = 1000, double interestRate = 0.2, int gracePeriodDays = 30)
        {
            ValidateUserAccess(ownerId);
            return db.CreateCreditAccount(ownerId, creditLimit, interestRate, gracePeriodDays);
        }

        public List<Account> GetUserAccounts(int userId)
        {
            ValidateUserAccess(userId);
            return db.GetUserAccounts(userId);
        }

        public Account GetAccountById(int accountId)
        {
            Account account = db.GetAccountById(accountId);
            if (account != null)
            {
                ValidateUserAccess(account.OwnerId);
            }
            return account;
        }

        public double DepositToAccount(int accountId, double amount)
        {
            Account account = db.GetAccountById(accountId);
            if (account == null)
                throw new ArgumentException("Account not found");

            ValidateUserAccess(account.OwnerId);

            double newBalance = account.Deposit(amount);
            db.UpdateAccountBalance(accountId, newBalance);
            return newBalance;
        }

        public double WithdrawFromAccount(int accountId, double amount)
        {
            Account account = db.GetAccountById(accountId);
            if (account == null)
                throw new ArgumentException("Account not found");

            ValidateUserAccess(account.OwnerId);

            double newBalance = account.Withdraw(amount);
            db.UpdateAccountBalance(accountId, newBalance);
            return newBalance;
        }

        public bool LinkCheckingAndSavings(int checkingAccountId, int savingsAccountId)
        {
            Account checkingAccount = db.GetAccountById(checkingAccountId);
            Account savingsAccount = db.GetAccountById(savingsAccountId);

            if (checkingAccount == null || savingsAccount == null)
                throw new ArgumentException("One or both accounts not found");

            if (!(checkingAccount is CheckingAccount) || !(savingsAccount is SavingsAccount))
                throw new ArgumentException("Invalid account types");

            ValidateUserAccess(checkingAccount.OwnerId);
            ValidateUserAccess(savingsAccount.OwnerId);

            if (checkingAccount.OwnerId != savingsAccount.OwnerId)
                throw new InvalidOperationException("Accounts must belong to the same owner");

            return db.LinkSavingsAccount(checkingAccountId, savingsAccountId);
        }

        public double TransferBetweenAccounts(int fromAccountId, int toAccountId, double amount)
        {
            Account fromAccount = db.GetAccountById(fromAccountId);
            Account toAccount = db.GetAccountById(toAccountId);

            if (fromAccount == null || toAccount == null)
                throw new ArgumentException("One or both accounts not found");

            ValidateUserAccess(fromAccount.OwnerId);

            // For transfers to others' accounts, only validate from account ownership
            if (fromAccount.OwnerId != toAccount.OwnerId && (currentUser.Role != User.UserRole.Banker && currentUser.Role != User.UserRole.Admin))
            {
                throw new UnauthorizedAccessException("Cannot transfer to another user's account");
            }

            double fromBalance = fromAccount.Withdraw(amount);
            db.UpdateAccountBalance(fromAccountId, fromBalance);

            double toBalance = toAccount.Deposit(amount);
            db.UpdateAccountBalance(toAccountId, toBalance);

            LogTransaction(fromAccountId, "Transfer Out", -amount, fromBalance);
            LogTransaction(toAccountId, "Transfer In", amount, toBalance);

            return fromBalance;
        }

        public double CalculateAccountInterest(int accountId)
        {
            Account account = db.GetAccountById(accountId);
            if (account == null)
                throw new ArgumentException("Account not found");

            ValidateUserAccess(account.OwnerId);

            // Use simulated time if enabled
            DateTime currentTime = TimeSimulator.CurrentTime;
            if (TimeSimulator.IsSimulationEnabled)
            {
                Console.WriteLine($"Calculating interest at simulated time: {currentTime:yyyy-MM-dd HH:mm}");
            }

            double interest = account.CalculateMonthlyInterest();
            if (interest != 0)
            {
                db.UpdateAccountBalance(accountId, account.GetBalance());
                Console.WriteLine($"Interest calculated: {interest:C}");
            }
            else if (account is CreditAccount credit && currentTime <= credit.GracePeriodEnd)
            {
                Console.WriteLine($"Account is in grace period until {credit.GracePeriodEnd:yyyy-MM-dd}");
            }

            return interest;
        }

        public bool CloseAccount(int accountId)
        {
            Account account = db.GetAccountById(accountId);
            if (account == null)
                throw new ArgumentException("Account not found");

            ValidateUserAccess(account.OwnerId);

            if (account.GetBalance() != 0)
                throw new InvalidOperationException("Account must have zero balance to close");

            return db.CloseAccount(accountId);
        }

        public List<Tuple<DateTime, string, double, double>> GetAccountTransactionHistory(int accountId)
        {
            Account account = db.GetAccountById(accountId);
            if (account == null)
                throw new ArgumentException("Account not found");

            ValidateUserAccess(account.OwnerId);

            return db.GetAccountTransactions(accountId);
        }

        // Admin/Banker methods
        public List<Account> GetAllAccounts()
        {
            if (currentUser == null || (currentUser.Role != User.UserRole.Admin && currentUser.Role != User.UserRole.Banker))
            {
                throw new UnauthorizedAccessException("Admin or Banker access required");
            }

            return db.GetAllAccounts();
        }

        // Helper methods
        private void ValidateUserAccess(int userId)
        {
            // Ensure user is logged in
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("User not logged in");
            }

            // Admin and Banker can access any account
            if (currentUser.Role == User.UserRole.Admin || currentUser.Role == User.UserRole.Banker)
            {
                return;
            }

            // Regular users can only access their own accounts
            if (currentUser.UserId != userId)
            {
                throw new UnauthorizedAccessException("You can only manage your own accounts");
            }
        }

        public static void LogTransaction(int accountId, string type, double amount, double newBalance)
        {
            db.LogTransactionToDB(accountId, type, amount, newBalance);
        }
    }

    // Program class with main method
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Initialize logger and start the application
                Logger.Initialize();
                Logger.Info("Application Starting");

                // Initialize database and bank system
                string dbPath = "bank.db";
                bool resetDatabase = false;
                bool isNewDatabase = !File.Exists(dbPath);

                if (!isNewDatabase)
                {
                    Console.Write("\nDatabase already exists. Do you want to reset it and start fresh? (y/N): ");
                    string response = Console.ReadLine()?.ToLower();
                    resetDatabase = response == "y";

                    if (resetDatabase)
                    {
                        Console.WriteLine("Database will be reset. Starting with fresh database.");
                    }
                    else
                    {
                        Console.WriteLine("Using existing database. All previous data will be preserved.");
                    }
                }

                BankSystem bank = new BankSystem(dbPath, resetDatabase);

                // Show admin credentials only for new or reset database
                if (isNewDatabase || resetDatabase)
                {
                    Console.WriteLine("\nDefault admin credentials: username='admin', password='admin'");
                }

                Console.WriteLine(new string('-', 50));
                Console.WriteLine("SSPŠ Banking System");
                Console.WriteLine(new string('-', 50));

                // Ask about database reset before anything else
                Console.WriteLine("1. Login");
                Console.WriteLine("2. Exit");
                Console.Write("Select an option: ");

                string input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        DoLogin(bank);
                        break;
                    case "2":
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Invalid option. Press any key to continue...");
                        Console.ReadKey();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Application terminated unexpectedly", ex);
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void DoLogin(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Login");
            Console.WriteLine(new string('-', 50));
            Console.Write("Username: ");
            string username = Console.ReadLine();
            Console.Write("Password: ");
            string password = ReadPassword();

            User user = bank.Login(username, password);
            if (user != null)
            {
                Console.WriteLine($"Welcome, {username}!");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();

                // Show appropriate menu based on user role
                while (user != null)
                {
                    switch (user.Role)
                    {
                        case User.UserRole.Admin:
                            ShowAdminMenu(bank);
                            break;
                        case User.UserRole.Banker:
                            ShowBankerMenu(bank);
                            break;
                        case User.UserRole.Client:
                            ShowClientMenu(bank);
                            break;
                    }
                    user = bank.GetCurrentUser();
                }
            }
            else
            {
                Console.WriteLine("Invalid username or password. Press any key to continue...");
                Console.ReadKey();
            }
        }

        static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            } while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return password;
        }

        static void ShowAdminMenu(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Admin Menu");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("1. Manage Users");
            Console.WriteLine("2. Manage Accounts");
            Console.WriteLine("3. Time Simulation");
            Console.WriteLine("4. Logout");
            Console.Write("Select an option: ");

            string input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    ShowUserManagementMenu(bank);
                    break;
                case "2":
                    ShowAccountManagementMenu(bank);
                    break;
                case "3":
                    ShowTimeSimulationMenu(bank);
                    break;
                case "4":
                    bank.Logout();
                    break;
                default:
                    Console.WriteLine("Invalid option. Press any key to continue...");
                    Console.ReadKey();
                    break;
            }
        }

        static void ShowBankerMenu(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Banker Menu");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("1. View All Accounts");
            Console.WriteLine("2. Create Account for Client");
            Console.WriteLine("3. Process Transaction");
            Console.WriteLine("4. Logout");
            Console.Write("Select an option: ");

            string input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    ShowAllAccounts(bank);
                    break;
                case "2":
                    CreateAccountForClient(bank);
                    break;
                case "3":
                    ProcessTransaction(bank);
                    break;
                case "4":
                    bank.Logout();
                    break;
                default:
                    Console.WriteLine("Invalid option. Press any key to continue...");
                    Console.ReadKey();
                    break;
            }
        }

        static void ShowClientMenu(BankSystem bank)
        {
            Console.Clear();
            User currentUser = bank.GetCurrentUser();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine($"Client Menu - {currentUser.Username}");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("1. View My Accounts");
            Console.WriteLine("2. Deposit");
            Console.WriteLine("3. Withdraw");
            Console.WriteLine("4. Transfer");
            Console.WriteLine("5. Transaction History");
            Console.WriteLine("6. Logout");
            Console.Write("Select an option: ");

            string input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    ShowUserAccounts(bank, currentUser.UserId);
                    break;
                case "2":
                    DoDeposit(bank);
                    break;
                case "3":
                    DoWithdraw(bank);
                    break;
                case "4":
                    DoTransfer(bank);
                    break;
                case "5":
                    ShowTransactionHistory(bank);
                    break;
                case "6":
                    bank.Logout();
                    break;
                default:
                    Console.WriteLine("Invalid option. Press any key to continue...");
                    Console.ReadKey();
                    break;
            }
        }

        static void ShowUserManagementMenu(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("User Management");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("1. List All Users");
            Console.WriteLine("2. Create New User");
            Console.WriteLine("3. Change User Role");
            Console.WriteLine("4. Delete User");
            Console.WriteLine("5. Back to Admin Menu");
            Console.Write("Select an option: ");

            string input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    ListAllUsers(bank);
                    break;
                case "2":
                    CreateNewUser(bank);
                    break;
                case "3":
                    ChangeUserRole(bank);
                    break;
                case "4":
                    DeleteUser(bank);
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Invalid option. Press any key to continue...");
                    Console.ReadKey();
                    break;
            }
        }

        static void ShowAccountManagementMenu(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Account Management");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("1. View All Accounts");
            Console.WriteLine("2. View Account Details");
            Console.WriteLine("3. Create New Account");
            Console.WriteLine("4. Process Transaction");
            Console.WriteLine("5. Close Account");
            Console.WriteLine("6. Back to Admin Menu");
            Console.Write("Select an option: ");

            string input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    ShowAllAccounts(bank);
                    break;
                case "2":
                    ViewAccountDetails(bank);
                    break;
                case "3":
                    CreateNewAccount(bank);
                    break;
                case "4":
                    ProcessTransaction(bank);
                    break;
                case "5":
                    CloseAccount(bank);
                    break;
                case "6":
                    return;
                default:
                    Console.WriteLine("Invalid option. Press any key to continue...");
                    Console.ReadKey();
                    break;
            }
        }

        static void ShowTimeSimulationMenu(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Time Simulation");
            Console.WriteLine(new string('-', 50));

            if (TimeSimulator.IsSimulationEnabled)
            {
                Console.WriteLine($"Current simulated time: {TimeSimulator.GetSimulatedTime:yyyy-MM-dd HH:mm}");
                Console.WriteLine("\nOptions:");
                Console.WriteLine("1. Advance time");
                Console.WriteLine("2. Disable simulation");
                Console.Write("Select an option: ");

                string input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        Console.Write("Enter number of days to advance: ");
                        if (int.TryParse(Console.ReadLine(), out int days))
                        {
                            TimeSimulator.AdvanceTime(days);
                            Console.WriteLine($"Time advanced by {days} days. New time: {TimeSimulator.GetSimulatedTime:yyyy-MM-dd HH:mm}");
                        }
                        else
                        {
                            Console.WriteLine("Invalid number of days.");
                        }
                        break;
                    case "2":
                        TimeSimulator.DisableSimulation();
                        Console.WriteLine("Time simulation disabled. Using real time.");
                        break;
                    default:
                        Console.WriteLine("Invalid option.");
                        break;
                }
            }
            else
            {
                Console.WriteLine("Time simulation is currently disabled.");
                Console.WriteLine("\nOptions:");
                Console.WriteLine("1. Enable simulation");
                Console.Write("Select an option: ");

                string input = Console.ReadLine();
                if (input == "1")
                {
                    Console.Write("Enter start date (yyyy-MM-dd): ");
                    if (DateTime.TryParse(Console.ReadLine(), out DateTime startDate))
                    {
                        TimeSimulator.EnableSimulation(startDate);
                        Console.WriteLine($"Time simulation enabled. Current time: {TimeSimulator.GetSimulatedTime:yyyy-MM-dd HH:mm}");
                    }
                    else
                    {
                        Console.WriteLine("Invalid date format.");
                    }
                }
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        // User management implementation
        static void ListAllUsers(BankSystem bank)
        {
            try
            {
                Console.Clear();
                Console.WriteLine(new string('-', 50));
                Console.WriteLine("All Users");
                Console.WriteLine(new string('-', 50));
                List<User> users = bank.GetAllUsers();

                if (users.Count == 0)
                {
                    Console.WriteLine("No users found.");
                }
                else
                {
                    Console.WriteLine($"{"ID",-5} {"Username",-15} {"Role",-10} {"Created At",-20}");
                    Console.WriteLine(new string('-', 50));

                    foreach (User user in users)
                    {
                        Console.WriteLine($"{user.UserId,-5} {user.Username,-15} {user.Role,-10} {user.CreatedAt.ToString("yyyy-MM-dd HH:mm"),-20}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void CreateNewUser(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Create New User");
            Console.WriteLine(new string('-', 50));

            try
            {
                Console.Write("Username: ");
                string username = Console.ReadLine();

                Console.Write("Password: ");
                string password = ReadPassword();

                Console.WriteLine("Select Role:");
                Console.WriteLine("1. Client");
                Console.WriteLine("2. Banker");
                Console.WriteLine("3. Admin");
                Console.Write("Role: ");
                string roleInput = Console.ReadLine();

                User.UserRole role = User.UserRole.Client;
                switch (roleInput)
                {
                    case "2":
                        role = User.UserRole.Banker;
                        break;
                    case "3":
                        role = User.UserRole.Admin;
                        break;
                }

                User newUser = bank.RegisterUser(username, password, role);
                Console.WriteLine($"User {username} created successfully with role {role}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void ChangeUserRole(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Change User Role");
            Console.WriteLine(new string('-', 50));

            try
            {
                ListAllUsers(bank);

                Console.Write("Enter User ID to change role: ");
                if (!int.TryParse(Console.ReadLine(), out int userId))
                {
                    Console.WriteLine("Invalid user ID.");
                    return;
                }

                Console.WriteLine("Select New Role:");
                Console.WriteLine("1. Client");
                Console.WriteLine("2. Banker");
                Console.WriteLine("3. Admin");
                Console.Write("New Role: ");
                string roleInput = Console.ReadLine();

                User.UserRole newRole = User.UserRole.Client;
                switch (roleInput)
                {
                    case "2":
                        newRole = User.UserRole.Banker;
                        break;
                    case "3":
                        newRole = User.UserRole.Admin;
                        break;
                }

                // Get the user by ID
                List<User> users = bank.GetAllUsers();
                User userToChange = users.FirstOrDefault(u => u.UserId == userId);
                if (userToChange == null)
                {
                    Console.WriteLine("User not found.");
                    return;
                }

                bool success = bank.ChangeUserRole(userToChange.Username, newRole);
                if (success)
                {
                    Console.WriteLine($"User {userToChange.Username} role changed to {newRole}.");
                }
                else
                {
                    Console.WriteLine($"Failed to change role for user {userToChange.Username}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void DeleteUser(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Delete User");
            Console.WriteLine(new string('-', 50));

            try
            {
                ListAllUsers(bank);

                Console.Write("Enter User ID to delete: ");
                if (!int.TryParse(Console.ReadLine(), out int userId))
                {
                    Console.WriteLine("Invalid user ID.");
                    return;
                }

                // Get the user by ID
                List<User> users = bank.GetAllUsers();
                User userToDelete = users.FirstOrDefault(u => u.UserId == userId);
                if (userToDelete == null)
                {
                    Console.WriteLine("User not found.");
                    return;
                }

                Console.Write($"Are you sure you want to delete user {userToDelete.Username}? (y/n): ");
                string confirm = Console.ReadLine().ToLower();

                if (confirm == "y" || confirm == "yes")
                {
                    bool success = bank.DeleteUserAccount(userToDelete.Username);
                    if (success)
                    {
                        Console.WriteLine($"User {userToDelete.Username} deleted successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to delete user {userToDelete.Username}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        // Account management implementation
        static void ShowAllAccounts(BankSystem bank)
        {
            try
            {
                Console.Clear();
                Console.WriteLine(new string('-', 50));
                Console.WriteLine("All Accounts");
                Console.WriteLine(new string('-', 50));
                List<Account> accounts = bank.GetAllAccounts();

                if (accounts.Count == 0)
                {
                    Console.WriteLine("No accounts found.");
                }
                else
                {
                    Console.WriteLine($"{"ID",-5} | {"Type",-25} | {"Owner ID",-8} | {"Balance",-15} | {"Created At",-20}");
                    Console.WriteLine(new string('-', 80));

                    foreach (Account account in accounts)
                    {
                        Console.WriteLine(
                            $"{account.AccountId,-5} | " +
                            $"{account.GetAccountType(),-25} | " +
                            $"{account.OwnerId,-8} | " +
                            $"{account.GetBalance():N2} Kč".PadRight(15) + " | " +
                            $"{account.CreationDate:yyyy-MM-dd HH:mm}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void ViewAccountDetails(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("View Account Details");
            Console.WriteLine(new string('-', 50));

            try
            {
                Console.Write("Enter Account ID: ");
                if (int.TryParse(Console.ReadLine(), out int accountId))
                {
                    Account account = bank.GetAccountById(accountId);
                    if (account != null)
                    {
                        Console.WriteLine("\nAccount Details:");
                        Console.WriteLine($"ID: {account.AccountId}");
                        Console.WriteLine($"Type: {account.GetAccountType()}");
                        Console.WriteLine($"Owner ID: {account.OwnerId}");
                        Console.WriteLine($"Balance: {account.GetBalance():C}");
                        Console.WriteLine($"Created At: {account.CreationDate:yyyy-MM-dd HH:mm}");

                        if (account is SavingsAccount savings)
                        {
                            Console.WriteLine($"Interest Rate: {savings.InterestRate:P}");
                            Console.WriteLine($"Daily Withdrawal Limit: {savings.DailyWithdrawalLimit:C}");
                        }
                        else if (account is StudentSavingsAccount studentSavings)
                        {
                            Console.WriteLine($"Interest Rate: {studentSavings.InterestRate:P}");
                            Console.WriteLine($"Daily Withdrawal Limit: {studentSavings.DailyWithdrawalLimit:C}");
                            Console.WriteLine($"Single Withdrawal Limit: {studentSavings.SingleWithdrawalLimit:C}");
                        }
                        else if (account is CreditAccount credit)
                        {
                            Console.WriteLine($"Credit Limit: {credit.CreditLimit:C}");
                            Console.WriteLine($"Interest Rate: {credit.InterestRate:P}");
                            Console.WriteLine($"Grace Period End: {credit.GracePeriodEnd:yyyy-MM-dd}");
                        }
                        else if (account is CheckingAccount checking)
                        {
                            if (checking.LinkedSavingsAccountId.HasValue)
                            {
                                Console.WriteLine($"Linked Savings Account ID: {checking.LinkedSavingsAccountId}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Account not found.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid account ID.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void CreateNewAccount(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Create New Account");
            Console.WriteLine(new string('-', 50));

            try
            {
                Console.Write("Enter Owner ID: ");
                if (!int.TryParse(Console.ReadLine(), out int ownerId))
                {
                    Console.WriteLine("Invalid owner ID.");
                    return;
                }

                Console.WriteLine("\nSelect Account Type:");
                Console.WriteLine("1. Checking Account");
                Console.WriteLine("2. Savings Account");
                Console.WriteLine("3. Student Savings Account");
                Console.WriteLine("4. Credit Account");
                Console.Write("Type: ");

                string typeInput = Console.ReadLine();
                int accountId = 0;

                switch (typeInput)
                {
                    case "1":
                        Console.Write("Initial Deposit: ");
                        if (double.TryParse(Console.ReadLine(), out double initialDeposit))
                        {
                            accountId = bank.CreateCheckingAccount(ownerId, initialDeposit);
                        }
                        break;

                    case "2":
                        Console.Write("Initial Deposit: ");
                        if (double.TryParse(Console.ReadLine(), out double savingsDeposit))
                        {
                            accountId = bank.CreateSavingsAccount(ownerId, savingsDeposit);
                        }
                        break;

                    case "3":
                        Console.Write("Initial Deposit: ");
                        if (double.TryParse(Console.ReadLine(), out double studentDeposit))
                        {
                            accountId = bank.CreateStudentSavingsAccount(ownerId, studentDeposit);
                        }
                        break;

                    case "4":
                        Console.Write("Credit Limit: ");
                        if (double.TryParse(Console.ReadLine(), out double creditLimit))
                        {
                            accountId = bank.CreateCreditAccount(ownerId, creditLimit);
                        }
                        break;

                    default:
                        Console.WriteLine("Invalid account type.");
                        return;
                }

                if (accountId > 0)
                {
                    Console.WriteLine($"Account created successfully with ID: {accountId}");
                }
                else
                {
                    Console.WriteLine("Failed to create account.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void ProcessTransaction(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Process Transaction");
            Console.WriteLine(new string('-', 50));

            try
            {
                Console.Write("Enter Account ID: ");
                if (!int.TryParse(Console.ReadLine(), out int accountId))
                {
                    Console.WriteLine("Invalid account ID.");
                    return;
                }

                Console.WriteLine("\nSelect Transaction Type:");
                Console.WriteLine("1. Deposit");
                Console.WriteLine("2. Withdraw");
                Console.Write("Type: ");

                string typeInput = Console.ReadLine();
                Console.Write("Amount: ");

                if (double.TryParse(Console.ReadLine(), out double amount))
                {
                    double newBalance = 0;
                    switch (typeInput)
                    {
                        case "1":
                            newBalance = bank.DepositToAccount(accountId, amount);
                            Console.WriteLine($"Deposit successful. New balance: {newBalance:C}");
                            break;

                        case "2":
                            newBalance = bank.WithdrawFromAccount(accountId, amount);
                            Console.WriteLine($"Withdrawal successful. New balance: {newBalance:C}");
                            break;

                        default:
                            Console.WriteLine("Invalid transaction type.");
                            return;
                    }
                }
                else
                {
                    Console.WriteLine("Invalid amount.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void CloseAccount(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Close Account");
            Console.WriteLine(new string('-', 50));

            try
            {
                Console.Write("Enter Account ID: ");
                if (int.TryParse(Console.ReadLine(), out int accountId))
                {
                    bool success = bank.CloseAccount(accountId);
                    if (success)
                    {
                        Console.WriteLine("Account closed successfully.");
                    }
                    else
                    {
                        Console.WriteLine("Failed to close account. Make sure the balance is zero.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid account ID.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void ShowUserAccounts(BankSystem bank, int userId)
        {
            try
            {
                Console.Clear();
                Console.WriteLine(new string('-', 50));
                Console.WriteLine("My Accounts");
                Console.WriteLine(new string('-', 50));
                List<Account> accounts = bank.GetUserAccounts(userId);

                if (accounts.Count == 0)
                {
                    Console.WriteLine("No accounts found.");
                }
                else
                {
                    // Group accounts by type for better organization
                    var groupedAccounts = accounts.GroupBy(a => a.GetAccountType());

                    foreach (var group in groupedAccounts)
                    {
                        Console.WriteLine($"\n{group.Key}:");
                        Console.WriteLine(new string('-', 80));
                        Console.WriteLine($"{"ID",-5} | {"Balance",-15} | {"Interest Rate",-15} | {"Created At",-20}");
                        Console.WriteLine(new string('-', 80));

                        foreach (Account account in group)
                        {
                            string interestRate = "N/A";
                            string additionalInfo = "";

                            if (account is SavingsAccount savings)
                            {
                                interestRate = $"{savings.InterestRate:P}";
                                additionalInfo = $"Daily Limit: {savings.DailyWithdrawalLimit:C}";
                            }
                            else if (account is StudentSavingsAccount studentSavings)
                            {
                                interestRate = $"{studentSavings.InterestRate:P}";
                                additionalInfo = $"Daily Limit: {studentSavings.DailyWithdrawalLimit:C}, Single Limit: {studentSavings.SingleWithdrawalLimit:C}";
                            }
                            else if (account is CreditAccount credit)
                            {
                                interestRate = $"{credit.InterestRate:P}";
                                double debt = credit.GetDebt();
                                additionalInfo = $"Credit Limit: {credit.CreditLimit:C}, Debt: {debt:C}";
                            }

                            Console.WriteLine(
                                $"{account.AccountId,-5} | " +
                                $"{account.GetBalance():N2} Kč".PadRight(15) + " | " +
                                $"{interestRate,-15} | " +
                                $"{account.CreationDate:yyyy-MM-dd HH:mm}"
                            );

                            if (!string.IsNullOrEmpty(additionalInfo))
                            {
                                Console.WriteLine($"     Additional Info: {additionalInfo}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void DoDeposit(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Deposit");
            Console.WriteLine(new string('-', 50));

            try
            {
                ShowUserAccounts(bank, bank.GetCurrentUser().UserId);

                Console.Write("\nEnter Account ID: ");
                if (!int.TryParse(Console.ReadLine(), out int accountId))
                {
                    Console.WriteLine("Invalid account ID.");
                    return;
                }

                Console.Write("Enter Amount: ");
                if (double.TryParse(Console.ReadLine(), out double amount))
                {
                    double newBalance = bank.DepositToAccount(accountId, amount);
                    Console.WriteLine($"Deposit successful. New balance: {newBalance:C}");
                }
                else
                {
                    Console.WriteLine("Invalid amount.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void DoWithdraw(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Withdraw");
            Console.WriteLine(new string('-', 50));

            try
            {
                ShowUserAccounts(bank, bank.GetCurrentUser().UserId);

                Console.Write("\nEnter Account ID: ");
                if (!int.TryParse(Console.ReadLine(), out int accountId))
                {
                    Console.WriteLine("Invalid account ID.");
                    return;
                }

                Console.Write("Enter Amount: ");
                if (double.TryParse(Console.ReadLine(), out double amount))
                {
                    double newBalance = bank.WithdrawFromAccount(accountId, amount);
                    Console.WriteLine($"Withdrawal successful. New balance: {newBalance:C}");
                }
                else
                {
                    Console.WriteLine("Invalid amount.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void DoTransfer(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Transfer");
            Console.WriteLine(new string('-', 50));

            try
            {
                ShowUserAccounts(bank, bank.GetCurrentUser().UserId);

                Console.Write("\nEnter From Account ID: ");
                if (!int.TryParse(Console.ReadLine(), out int fromAccountId))
                {
                    Console.WriteLine("Invalid account ID.");
                    return;
                }

                Console.Write("Enter To Account ID: ");
                if (!int.TryParse(Console.ReadLine(), out int toAccountId))
                {
                    Console.WriteLine("Invalid account ID.");
                    return;
                }

                Console.Write("Enter Amount: ");
                if (double.TryParse(Console.ReadLine(), out double amount))
                {
                    double newBalance = bank.TransferBetweenAccounts(fromAccountId, toAccountId, amount);
                    Console.WriteLine($"Transfer successful. New balance: {newBalance:C}");
                }
                else
                {
                    Console.WriteLine("Invalid amount.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void ShowTransactionHistory(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Transaction History");
            Console.WriteLine(new string('-', 50));

            try
            {
                ShowUserAccounts(bank, bank.GetCurrentUser().UserId);

                Console.Write("\nEnter Account ID: ");
                if (!int.TryParse(Console.ReadLine(), out int accountId))
                {
                    Console.WriteLine("Invalid account ID.");
                    return;
                }

                Account account = bank.GetAccountById(accountId);
                if (account == null)
                {
                    Console.WriteLine("Account not found.");
                    return;
                }

                // Display account summary
                Console.WriteLine("\nAccount Summary:");
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"Type: {account.GetAccountType()}");
                Console.WriteLine($"Current Balance: {account.GetBalance():C}");

                if (account is SavingsAccount savings)
                {
                    Console.WriteLine($"Interest Rate: {savings.InterestRate:P}");
                    Console.WriteLine($"Daily Withdrawal Limit: {savings.DailyWithdrawalLimit:C}");
                }
                else if (account is StudentSavingsAccount studentSavings)
                {
                    Console.WriteLine($"Interest Rate: {studentSavings.InterestRate:P}");
                    Console.WriteLine($"Daily Withdrawal Limit: {studentSavings.DailyWithdrawalLimit:C}");
                    Console.WriteLine($"Single Withdrawal Limit: {studentSavings.SingleWithdrawalLimit:C}");
                }
                else if (account is CreditAccount credit)
                {
                    Console.WriteLine($"Credit Limit: {credit.CreditLimit:C}");
                    Console.WriteLine($"Interest Rate: {credit.InterestRate:P}");
                    Console.WriteLine($"Current Debt: {credit.GetDebt():C}");
                    Console.WriteLine($"Grace Period End: {credit.GracePeriodEnd:yyyy-MM-dd}");
                }

                Console.WriteLine(new string('-', 80));

                List<Tuple<DateTime, string, double, double>> transactions = bank.GetAccountTransactionHistory(accountId);

                if (transactions.Count == 0)
                {
                    Console.WriteLine("\nNo transactions found.");
                }
                else
                {
                    Console.WriteLine("\nTransaction History:");
                    Console.WriteLine(new string('-', 80));
                    Console.WriteLine($"{"Date",-20} | {"Type",-15} | {"Amount",-15} | {"Balance",-15}");
                    Console.WriteLine(new string('-', 80));

                    foreach (var transaction in transactions)
                    {
                        string amountColor = transaction.Item3 >= 0 ? "green" : "red";
                        Console.WriteLine(
                            $"{transaction.Item1:yyyy-MM-dd HH:mm}".PadRight(20) + " | " +
                            $"{transaction.Item2,-15} | " +
                            $"{transaction.Item3:N2} Kč".PadRight(15) + " | " +
                            $"{transaction.Item4:N2} Kč".PadRight(15)
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        static void CreateAccountForClient(BankSystem bank)
        {
            Console.Clear();
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Create Account for Client");
            Console.WriteLine(new string('-', 50));

            try
            {
                // First, show all users to help the banker select a client
                Console.WriteLine("\nAvailable Clients:");
                List<User> users = bank.GetAllUsers();
                var clients = users.Where(u => u.Role == User.UserRole.Client).ToList();

                if (clients.Count == 0)
                {
                    Console.WriteLine("No clients found.");
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"{"ID",-5} {"Username",-15} {"Created At",-20}");
                Console.WriteLine(new string('-', 40));

                foreach (User client in clients)
                {
                    Console.WriteLine($"{client.UserId,-5} {client.Username,-15} {client.CreatedAt.ToString("yyyy-MM-dd HH:mm"),-20}");
                }

                Console.Write("\nEnter Client ID: ");
                if (!int.TryParse(Console.ReadLine(), out int clientId))
                {
                    Console.WriteLine("Invalid client ID.");
                    return;
                }

                // Verify the selected user is a client
                User selectedUser = clients.FirstOrDefault(u => u.UserId == clientId);
                if (selectedUser == null || selectedUser.Role != User.UserRole.Client)
                {
                    Console.WriteLine("Invalid client ID or user is not a client.");
                    return;
                }

                Console.WriteLine("\nSelect Account Type:");
                Console.WriteLine("1. Checking Account");
                Console.WriteLine("2. Savings Account");
                Console.WriteLine("3. Student Savings Account");
                Console.WriteLine("4. Credit Account");
                Console.Write("Type: ");

                string typeInput = Console.ReadLine();
                int accountId = 0;

                switch (typeInput)
                {
                    case "1":
                        Console.Write("Initial Deposit: ");
                        if (double.TryParse(Console.ReadLine(), out double initialDeposit))
                        {
                            accountId = bank.CreateCheckingAccount(clientId, initialDeposit);
                        }
                        break;

                    case "2":
                        Console.Write("Initial Deposit: ");
                        if (double.TryParse(Console.ReadLine(), out double savingsDeposit))
                        {
                            accountId = bank.CreateSavingsAccount(clientId, savingsDeposit);
                        }
                        break;

                    case "3":
                        Console.Write("Initial Deposit: ");
                        if (double.TryParse(Console.ReadLine(), out double studentDeposit))
                        {
                            accountId = bank.CreateStudentSavingsAccount(clientId, studentDeposit);
                        }
                        break;

                    case "4":
                        Console.Write("Credit Limit: ");
                        if (double.TryParse(Console.ReadLine(), out double creditLimit))
                        {
                            accountId = bank.CreateCreditAccount(clientId, creditLimit);
                        }
                        break;

                    default:
                        Console.WriteLine("Invalid account type.");
                        return;
                }

                if (accountId > 0)
                {
                    Console.WriteLine($"Account created successfully with ID: {accountId}");
                }
                else
                {
                    Console.WriteLine("Failed to create account.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
    }

    public class Logger
    {
        private static string logFile;
        private static object lockObj = new object();

        public static void Initialize(string filename = "logs.txt")
        {
            try
            {
                // Vytvoříme soubor v adresáři aplikace
                logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);

                // Vytvoříme soubor pokud neexistuje (nebo připravíme pro append pokud existuje)
                using (FileStream fs = new FileStream(logFile, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    fs.Close();
                }

                // Vypíšeme cestu k log souboru
                Console.WriteLine($"Log file location: {logFile}");

                // Přidáme oddělovací čáru při startu aplikace
                Log("INFO", "----------------------------------------");
                Log("INFO", "Application session started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
            }
        }

        public static void Log(string level, string message)
        {
            try
            {
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                Console.WriteLine(logMessage);

                if (!string.IsNullOrEmpty(logFile))
                {
                    lock (lockObj)
                    {
                        // Připojíme log na konec souboru
                        File.AppendAllText(logFile, logMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write log: {ex.Message}");
            }
        }

        public static void Info(string message) => Log("INFO", message);
        public static void Warning(string message) => Log("WARN", message);
        public static void Error(string message) => Log("ERROR", message);
        public static void Error(string message, Exception ex) => Log("ERROR", $"{message}\nException: {ex.Message}\nStack Trace: {ex.StackTrace}");
    }
}
