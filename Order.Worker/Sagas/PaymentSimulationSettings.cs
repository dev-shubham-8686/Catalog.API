namespace Order.Worker.Sagas
{
    public class PaymentSimulationSettings
    {
        /// <summary>
        /// Percentage (0-100) of simulated payment charges that fail, so the compensating
        /// (ReleaseStockIntegrationEvent) path is actually exercisable without a real payment
        /// provider. Defaults to 10% — enough to observe compensation happening under normal
        /// traffic without dominating the happy path.
        /// </summary>
        public int FailureRatePercent { get; set; } = 10;
    }
}
