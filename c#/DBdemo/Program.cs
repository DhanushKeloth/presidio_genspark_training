
using Npgsql;
using System.Data;
using System.Linq.Expressions;
using System.Net.NetworkInformation;

namespace UnderstandingADOApp
{
    
    internal class Program
    {
        static string ConnectionString =
            "Host=localhost;Port=5432;Database=practice;Username=dhanushkeloth;Password=1234";
        static NpgsqlConnection connection;
        public Program()
        {
             connection = new NpgsqlConnection(ConnectionString);
            
        }
        public  static void GetDataFromDB()
        {
            string query = "select * from employees;";
            NpgsqlCommand cmd = new NpgsqlCommand(query,connection);
            try
            {
                connection.Open();
                NpgsqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    // Console.WriteLine($"id : {reader[0]} name: {reader[1]}");
                   Console.WriteLine(reader["name"]);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"{ex.Message}");

            }
            finally
            {
                connection.Close();
            }

        }
        static Employee getUserDatafromConsole()
        {
            Employee e = new Employee();
            Console.WriteLine("enter the id");
            e.Id = Convert.ToInt32(Console.ReadLine());
            Console.WriteLine("enter the name");
            e.Name = Console.ReadLine()??"";
            return e;
        }
        static void InsertValues()
        {
            Employee e = getUserDatafromConsole();
            
            // string query = $"insert into employees (id,name) values ('{e.Id}','{e.Name}');";
            string query = "insert into employees (id,name) values (@id,@name);";

            NpgsqlCommand cmd = new NpgsqlCommand(query,connection);
            cmd.Parameters.AddWithValue("id",e.Id);
            cmd.Parameters.AddWithValue("name",e.Name);
            try
            {
                connection.Open();
                int rowsaffected = cmd.ExecuteNonQuery();
                if(rowsaffected>0) Console.WriteLine("successfully inserted values");

            }   
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                connection.Close();
            }
        }
        static void UpdateValues()
        {
            Console.WriteLine("enter the updated details of user with his id");
            Employee e = getUserDatafromConsole();
            
            string query=$"update employees set name='{e.Name}' where id={e.Id}";
            NpgsqlCommand cmd = new NpgsqlCommand(query,connection);

            try
            {
                connection.Open();
                int updatedrows = cmd.ExecuteNonQuery();
                if (updatedrows > 0)
                {
                    Console.WriteLine($"updated the row with id {e.Id}");
                }    
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                connection.Close();
            }
        }
        static void DeleteEmployeeWithId()
        {
            int id = Convert.ToInt32(Console.ReadLine());
            string query = "delete from employees where id=@id";
            NpgsqlCommand cmd = new NpgsqlCommand(query,connection);
            cmd.Parameters.AddWithValue("id",id);
            try
            {
                connection.Open();
                int rowsaffected = cmd.ExecuteNonQuery();
                if(rowsaffected>0) Console.WriteLine("deleted the employee with id"+id);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                connection.Close();
            }
        }
        static void DisplayDataUsingDataSet()
        {
            DataSet dataSet = new DataSet();
            string selectquery = "select * from employees";
            NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
            NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(selectquery,conn);
            try
            {
                Console.WriteLine(conn.State);
                adapter.Fill(dataSet,"employeetable");
                Console.WriteLine("data pulled to memory, connection is closed");
                DataTable table = dataSet.Tables["employeetable"];
                foreach(DataRow row in table.Rows)
                {
                    Console.WriteLine($"id: {row["id"]} name:{row["name"]}");
                }
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
        }
        static void Main(string[] args)
        {
            // Console.WriteLine("Hello, World!");
            new Program();
            // GetDataFromDB();
            // InsertValues();
            // UpdateValues();
            // DeleteEmployeeWithId();
            DisplayDataUsingDataSet();

        }
    }
}
public class Employee
{
    public string Name{get;set;}
    public int Id{get;set;}
}