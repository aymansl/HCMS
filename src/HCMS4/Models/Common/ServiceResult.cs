namespace HCMS4.Models.Common
{
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ServiceResult Ok(string message = null)
        {
            return new ServiceResult { Success = true, Message = message };
        }

        public static ServiceResult Fail(string message, List<string> errors = null)
        {
            return new ServiceResult 
            { 
                Success = false, 
                Message = message,
                Errors = errors ?? new List<string>()
            };
        }
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T Data { get; set; }

        public static ServiceResult<T> Ok(T data, string message = null)
        {
            return new ServiceResult<T> { Success = true, Message = message, Data = data };
        }

        public new static ServiceResult<T> Fail(string message, List<string> errors = null)
        {
            return new ServiceResult<T> 
            { 
                Success = false, 
                Message = message,
                Errors = errors ?? new List<string>()
            };
        }
    }
}
