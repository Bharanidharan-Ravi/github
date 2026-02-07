import { Suspense } from "react";
import ProjectLayout from "../components/Projects/Layout/ProjectLayout";
import { Route, Routes } from "react-router-dom";
import ViewProject from "../components/Projects/Components/ViewProject";
import ProjectDetails from "../components/Projects/Components/ProjectDetails";
import ProjectCreate from "../components/Projects/Components/ProjectCreation";

export default function ProjectRouter({ role }) {

    return (
        <Suspense fallback={<div>Loading...</div>}>
            <Routes>
                <Route element={<ProjectLayout />}>
                    <Route path="/" element={<ViewProject role={role} />} />
                    <Route path=":projId" element={<ProjectDetails />} />
                    <Route path="create" element={<ProjectCreate />} />
                </Route>
            </Routes>
        </Suspense>
    );
}
