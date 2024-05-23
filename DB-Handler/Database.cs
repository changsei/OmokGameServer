using Model;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DB_Handler
{
    public class DBHandler
    {
        private static DBHandler _instance;
        private static readonly object _lock = new object();
        private readonly string _connectionText = "Server=localhost\\SQLEXPRESS;Database=gameDB;Trusted_Connection=True";
        private SqlConnection _connection;

        private DBHandler() { }
        public static DBHandler Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new DBHandler();
                    }
                    return _instance;
                }
            }
        }
        public void ConnectToDB()
        {
            try
            {
                _connection = new SqlConnection(_connectionText);
                _connection.Open();
                Logger.Instance.Log(Logger.LogLevel.Info, "[데이터베이스]: 연결됨");
            }
            catch (SqlException e)
            {
                Logger.Instance.Log(Logger.LogLevel.Error, e.ToString());
            }
        }

        public bool Insert(User user)
        {
            try
            {
                string commandText = "INSERT INTO userTable (id, userName, password, phoneNumber) VALUES (@id, @userName, @password, @phoneNumber)";
                using (SqlCommand command = new SqlCommand(commandText, _connection))
                {
                    command.Parameters.AddWithValue("@id", user.ID.ToString());
                    command.Parameters.AddWithValue("@userName", user.Name.ToString());
                    command.Parameters.AddWithValue("@password", user.Password.ToString());
                    command.Parameters.AddWithValue("@phoneNumber", user.PhoneNumber.ToString());
                    int result = command.ExecuteNonQuery();
                    return result > 0;
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        public bool Delete(User user)
        {
            try
            {
                string commandText = "DELETE FROM userTable WHERE id = @id";
                using (SqlCommand command = new SqlCommand(commandText, _connection))
                {
                    command.Parameters.AddWithValue("@id", user.ID.ToString());
                    int result = command.ExecuteNonQuery();
                    return result > 0;
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        public bool Update(User user)
        {
            try
            {
                string commandText = "UPDATE userTable SET userName = @userName, password = @password, phoneNumber = @phoneNumber WHERE id = @id";
                using (SqlCommand command = new SqlCommand(commandText, _connection))
                {
                    command.Parameters.AddWithValue("@id", user.ID.ToString());
                    command.Parameters.AddWithValue("@userName", user.Name.ToString());
                    command.Parameters.AddWithValue("@password", user.Password.ToString());
                    command.Parameters.AddWithValue("@phoneNumber", user.PhoneNumber.ToString());
                    int result = command.ExecuteNonQuery();
                    return result > 0;
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        public User SearchByPhoneNumber(string phoneNumber)
        {
            try
            {
                string commandText = "SELECT * FROM userTable WHERE phoneNumber = @phoneNumber";
                using (SqlCommand command = new SqlCommand(commandText, _connection))
                {
                    command.Parameters.AddWithValue("@phoneNumber", phoneNumber.ToString());
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new User
                            {
                                ID = reader["id"].ToString().Trim(),
                                Name = "NULL",
                                Password = reader["password"].ToString().Trim(),
                                PhoneNumber = "NULL"
                            };
                        }

                        return null;
                    }
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public User SearchByIdForPassword(string userID)
        {
            try
            {
                string commandText = "SELECT * FROM userTable WHERE id = @id";
                using (SqlCommand command = new SqlCommand(commandText, _connection))
                {
                    command.Parameters.AddWithValue("@id", userID.ToString());
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new User
                            {
                                ID = reader["id"].ToString().Trim(),
                                Name = "NULL",
                                Password = reader["password"].ToString().Trim(),
                                PhoneNumber = "NULL"
                            };
                        }

                        return null;
                    }
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public List<User> SearchAll()
        {
            try
            {
                List<User> users = new List<User>();
                string commandText = "SELECT * FROM userTable";
                using (SqlCommand command = new SqlCommand(commandText, _connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                ID = reader["id"].ToString().Trim(),
                                Name = reader["userName"].ToString().Trim(),
                                Password = reader["password"].ToString().Trim(),
                                PhoneNumber = reader["phoneNumber"].ToString().Trim()
                            });
                        }
                    }
                }
                return users;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }
    }
}
