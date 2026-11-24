using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MF.Models
{
    public interface IPaymentService
    {
        Task<string> InitiateSessionAsync();
        public Task<GenericResponse<InitiatePaymentResponse>> InitiatePayment(InitiatePaymentRequest intiatePaymentRequest);
        public Task<GenericResponse<SendPaymentResponse>> SendPayment(SendPaymentRequest request);
        public Task<GenericResponse<ExecutePaymentResponse>> ExecutePayment(ExecutePaymentRequest executePaymentRequest); 
        public Task<GenericResponse<DirectPaymentResponse>> DirectPayment(DirectPaymentRequest directPaymentRequest);
        public Task<GenericResponse<bool>> CancelToken(string paymentToken);
        public Task<GenericResponse<bool>> CancelRecurringPayment(string recurringId);
        public Task<GenericResponse<GetPaymentStatusResponse>> GetPaymentStatus(GetPaymentStatusRequest getPaymentStatusRequest);


    }
}
