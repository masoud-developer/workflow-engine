import {API_ENDPOINT} from './configConsts';
import {COLLECTIONS} from './constants';
import cookie from 'react-cookies';

const axios = require('axios');

class API {
    constructor({url}) {
        this.url = url;
        this.endpoints = {};
    }

    /**
     * Create and store a single entity's endpoints
     * @param {A entity Object} entity
     */
    createEntity(entity) {
        this.endpoints[entity.name] = this.createBasicCRUDEndpoints(entity);
    }

    /**
     * Create the basic endpoints handlers for CRUD operations
     * @param {A entity Object} entity
     */
    createBasicCRUDEndpoints({name}) {
        const endpoints = {};

        const resouceURL = `${this.url}/${name}`;

        endpoints.getAll = (query) => axios.get(resouceURL, {
            params: query,
            auth: {username: cookie.load('access_token')}
        });

        endpoints.getOne = ({id}) => axios.get(`${resouceURL}/${id}`, {auth: {username: cookie.load('access_token')}});

        endpoints.getCustom = (body) => axios({url: resouceURL, ...body}).then(res => res.data);

        endpoints.create = (toCreate) => axios.post(resouceURL, toCreate, {auth: {username: cookie.load('access_token')}});

        endpoints.upload = (toCreate, config) => axios.post(resouceURL, toCreate, {auth: {username: cookie.load('access_token')}, ...config});

        endpoints.update = (toUpdate) => axios.put(`${resouceURL}/${toUpdate.id}`, toUpdate, {auth: {username: cookie.load('access_token')}});

        endpoints.delete = ({id}) => axios.delete(`${resouceURL}/${id}`, {auth: {username: cookie.load('access_token')}});

        return endpoints;
    }

    async getAccessToken() {
        let isSuccessfull = false;
        await this.get('refresh-token', {
            auth: {
                username: cookie.load('refresh_token')
            }
        }).then(response => {
            cookie.save('access_token', response.data.access_token, {path: '/'});
            cookie.save('refresh_token', response.data.refresh_token, {path: '/'});
            cookie.save('user', response.data.user, {path: '/'});
            cookie.save('settings', response.data.settings, {path: '/'});
            console.log("Successfully updated token");
            isSuccessfull = true;
        }).catch(() => {
            console.log("Failed to update token");
            isSuccessfull = false;
        });
        return isSuccessfull;
    }

    post = (path, body, config = {}) => {
        if(!config) config = {};
        const resourceURL = `${this.url}/${path}`;
        let accessToken = cookie.load('access_token');
        if (accessToken)
            config.auth = {username: cookie.load('access_token'), ...config.auth};
        return axios.post(resourceURL, body, config).then(res => res.data);
    }

    get = (path, config) => {
        if(!config) config = {};
        const resourceURL = `${this.url}/${path}`;
        let accessToken = cookie.load('access_token');
        if (accessToken)
            config.auth = {username: cookie.load('access_token'), ...config.auth};
        return axios.get(resourceURL, config).then(res => res.data);
    }
}

const workflowApi = new API({url: API_ENDPOINT});
workflowApi.createEntity({name: COLLECTIONS.TEMPLATES});
workflowApi.createEntity({name: COLLECTIONS.RUNS});
workflowApi.createEntity({name: COLLECTIONS.USERS});
workflowApi.createEntity({name: 'resource'});
workflowApi.createEntity({name: 'token'});
workflowApi.createEntity({name: 'register'});
workflowApi.createEntity({name: 'demo'});
workflowApi.createEntity({name: 'user_settings'});
workflowApi.createEntity({name: 'pull_settings'});
workflowApi.createEntity({name: 'worker_states'});
//workflowApi.createEntity({name: `search_${COLLECTIONS.TEMPLATES}`});
//workflowApi.createEntity({name: `search_${COLLECTIONS.RUNS}`});
workflowApi.createEntity({name: `search_in_hubs`});
workflowApi.createEntity({name: `upload_file`});
export const WorkflowApi = workflowApi;
