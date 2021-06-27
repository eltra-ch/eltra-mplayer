using MPlayerMaster.Device.Contracts;

namespace MPlayerMaster.Extensions
{
    internal static class PlannerExtension
    {
        public static string GetUrl(this PlannerComposition composition)
        {
            string result = string.Empty;

            if (composition != null && !string.IsNullOrEmpty(composition.FullPath))
            {
                result = $"\"{composition.FullPath}\"";
                /*result = $"file://{composition.FullPath}";
                result = result.Replace('\\', '/');
                result = result.Replace(" ", "%20");*/
            }

            return result;
        }

        public static string GetTitle(this PlannerComposition plannerComposition)
        {
            string result = string.Empty;

            if (plannerComposition != null && !string.IsNullOrEmpty(plannerComposition.FullPath))
            {
                var composition = plannerComposition.Composition;

                if(composition != null)
                {
                    result = composition.Title;
                }
            }

            return result;
        }
    }
}
