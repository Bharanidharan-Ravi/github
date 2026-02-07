import React, { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useRepoStore } from "../RepoStore/RepoStore";
import { useCustomStore, GetClientMaster, GetAllRepo, getProject } from "shared-store";
import "../repoDetails.css";
import { Folder } from "lucide-react";
import { ChevronRight } from "lucide-react";

const RepoDetails = () => {
  const isStandalone = !window.__MICRO_FRONTEND__;
  const { repoIds } = useParams();    // <-- you get the ID directly from URL
  const { RepoMaster, setRepoMaster, setProjMaster, projMaster,
    repoDetails, setRepoDetails
  } = useRepoStore();
  const Repo = useCustomStore(state => state.Repo);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchData = async () => {
      if (!Repo?.length) {
        const result = await GetAllRepo();
        setRepoMaster(result);
      } else {
        setRepoMaster(Repo);
      }
    };
    if (!RepoMaster || RepoMaster.length === 0) {
      fetchData(); // <-- runs only when repoMaster is empty
    }
  }, []);

  useEffect(() => {
    const fetchData = async () => {
      if (!projMaster || projMaster.length === 0) {
        const repoId = repoIds;
        const result = await getProject({ repoId });
        setProjMaster(result);
      }
    };
    fetchData();
  }, []);

  // 🔥 This runs ONLY when repoMaster updates (AFTER setRepoMaster)
  useEffect(() => {
    if (RepoMaster?.length) {
      const repo = RepoMaster.find(
        (item) => item.repo_Id === repoIds
      );
      setRepoDetails(repo);
    }
  }, [RepoMaster, repoIds]);

  const handleNavigateSmooth = () => {
    const basePath = window.location.pathname.split('/').slice(0, 2).join('/');  // /employee or /admin
    const relativePath = '/projects/create';
    let fullPath
    if (isStandalone) {

      fullPath = '/projects/create'
    } else {
      fullPath = `${basePath}${relativePath}`;
    }
    // Navigate to the full URL
    navigate(fullPath);
  };
  const handleRepoDetail = (projectid) => {
    console.log("its trigger ,", projectid);
    
    const basePath = window.location.pathname.split('/').slice(0, 2).join('/');  // /employee or /admin
    const relativePath = `/projects/${projectid}`;
    let fullPath
    if (isStandalone) {

      fullPath = `/projects/${projectid}`
    } else {
      fullPath = `${basePath}${relativePath}`;
    }
    // Navigate to the full URL
    navigate(fullPath);
  }
  return (
    <div className="repodetail-main container">
      <h2>{repoDetails.title}</h2>
      <div className="repodetail-data">
        <div className="repodetail-data-child">
          <p className="header-values">Client Name : <span className="values">{repoDetails.client_Name}</span></p>
          <p className="header-values">description : <span className="values">{repoDetails.description}</span> </p>
          <p className="header-values">Responsible : <span className="values">{repoDetails.ownerName}</span></p>
        </div>
        <div className="repodetail-data-child">
          <p>Client Mail-Id :</p>
          <p>Active From : {repoDetails.clientValidFrom}</p>
          <p>status : {repoDetails.status} </p>
        </div>
      </div>
      <div className="project-header">
        <h2>Projects</h2>
        <button onClick={handleNavigateSmooth}>Create Project</button>
      </div>
      <div className="repo-body">
        <ul className="ticket-list">
          {Array.isArray(projMaster) && projMaster?.map((proj) => (
            <li key={proj.id} 
            onClick={() => handleRepoDetail(proj.id)}
             className={`ticket ${proj.status}`}  >
              {/* <Link to={`${proj.id}`} className="ticket-link"> */}
              <div className="ticket-header">
                <div>
                  {<Folder size={24} />}
                  <span className="Repo-title">{proj.project_Name}</span>
                </div>
                <div>
                  <span>{proj.status}</span>
                  <ChevronRight className="repo-arrow" />
                </div>
              </div>
              <div className="list-body">
                <span>{proj.employeeName}</span>
                <span>{proj.dueDate}</span>
              </div>
              {/* </Link> */}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
};
export default RepoDetails;