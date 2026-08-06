import { createRouter, createWebHistory } from "vue-router";

const ScreeningListOverview = () => import("./views/ScreeningListOverview.vue");
const Recommendations = () => import("./views/RecommendationsList.vue");
const ResearchSubjectHistory = () => import("./views/ResearchSubjectHistory.vue");
const PatientRecord = () => import("./views/PatientRecord.vue");

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "recommendations",
      component: ScreeningListOverview,
      props: true,
    },
    {
      path: "/recommendations/:listId",
      name: "patient-recommendations-by-id",
      component: Recommendations,
      props: true,
    },
    {
      path: "/subjects/:subjectId/history",
      name: "researchsubject-history",
      component: ResearchSubjectHistory,
      props: true,
    },
    {
      path: "/patients/:patientId/record",
      name: "patient-record",
      component: PatientRecord,
      props: true,
    },
  ],
});

export default router;
