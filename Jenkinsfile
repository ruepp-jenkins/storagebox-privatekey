properties(
    [
        githubProjectProperty(
            displayName: 'storagebox-privatekey',
            projectUrlStr: 'https://github.com/ruepp-jenkins/storagebox-privatekey'
        ),
        disableConcurrentBuilds(abortPrevious: true)
    ]
)

pipeline {
    agent {
        label 'docker'
    }

    environment {
        IMAGE_FULLNAME = 'ruepp/storagebox-privatekey'
        DOCKER_PLATFORMS = 'linux/amd64,linux/arm64'
        DOCKER_API_PASSWORD = credentials('DOCKER_API_PASSWORD')
    }

    triggers {
        URLTrigger(
            cronTabSpec: 'H/30 * * * *',
            labelRestriction: 'urltrigger',
            entries: [
                URLTriggerEntry(
                    url: 'https://mcr.microsoft.com/v2/dotnet/sdk/manifests/9.0-alpine',
                    contentTypes: [
                        JsonContent(
                            [
                                JsonContentEntry(jsonPath: '$.protected')
                            ]
                        )
                    ]
                ),
                URLTriggerEntry(
                    url: 'https://mcr.microsoft.com/v2/dotnet/aspnet/manifests/9.0-alpine',
                    contentTypes: [
                        JsonContent(
                            [
                                JsonContentEntry(jsonPath: '$.protected')
                            ]
                        )
                    ]
                )
            ]
        )
    }

    stages {
        stage('Checkout') {
            steps {
                git branch: env.BRANCH_NAME,
                url: 'git@github.com:ruepp-jenkins/storagebox-privatekey.git',
                credentialsId: 'github.com-ssh'
            }
        }
        stage('Prepare Buildx') {
            steps {
                sh 'docker run --privileged --rm tonistiigi/binfmt --install arm64'
            }
        }
        stage('Build') {
            steps {
                script {
                    def rawBuilderName = "mybuilder-${env.JOB_NAME}-${env.BUILD_NUMBER}"
                    env.BUILDER_NAME = rawBuilderName.replaceAll('[^A-Za-z0-9_.-]', '-')
                    env.DATESTAMP = sh(script: 'date +%Y%m%d', returnStdout: true).trim()
                }
                sh 'chmod +x scripts/*.sh'
                sh './scripts/start.sh'
            }
        }
        stage('Verify Manifest') {
            steps {
                script {
                    def imageRef

                    if (env.BRANCH_NAME == 'master' || env.BRANCH_NAME == 'main') {
                        imageRef = "${env.IMAGE_FULLNAME}:latest"
                    } else {
                        imageRef = "${env.IMAGE_FULLNAME}-test:${env.BRANCH_NAME}-${env.DATESTAMP}"
                    }

                    withEnv(["IMAGE_REF=${imageRef}"]) {
                        sh '''
                            set -euo pipefail
                            echo "Verifying pushed manifest for ${IMAGE_REF}"
                            manifest_output="$(docker buildx imagetools inspect "${IMAGE_REF}")"
                            printf '%s\n' "${manifest_output}"

                            for platform in $(printf '%s' "${DOCKER_PLATFORMS}" | tr ',' ' '); do
                                if ! printf '%s\n' "${manifest_output}" | grep -Eq "Platform:[[:space:]]+${platform}"; then
                                    echo "Missing platform '${platform}' in ${IMAGE_REF}"
                                    exit 1
                                fi
                            done
                        '''
                    }
                }
            }
        }
    }

    post {
        always {
            discordSend result: currentBuild.currentResult,
                description: env.GIT_URL,
                link: env.BUILD_URL,
                title: JOB_NAME,
                webhookURL: DISCORD_WEBHOOK
            cleanWs()
        }
    }
}
